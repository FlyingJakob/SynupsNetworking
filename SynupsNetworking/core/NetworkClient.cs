using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using SynupsNetworking.components;
using SynupsNetworking.core.Enums;
using SynupsNetworking.core.Misc;
using SynupsNetworking.transport;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using Debug = UnityEngine.Debug;

namespace SynupsNetworking.core
{
    public class NetworkClient
    {
        [Header("Singleton")] public static NetworkClient instance;


        private readonly Transport transport;
        public bool isRendezvous { get; private set; }


        public int lastUsedClientID;
        
        public int clientID { get; private set; }

        private IPEndPoint privateEndpoint { get; set; }
        public readonly int serverID = 1337;
        public ConcurrentDictionary<int, Client> clientList { get; private set; } = new ConcurrentDictionary<int, Client>();

        public ConcurrentDictionary<IPEndPoint, ConnectionStatus> connectionStatus { get; private set; } = new ConcurrentDictionary<IPEndPoint, ConnectionStatus>();
        
        ConcurrentStack<(int, OwnershipCheck)> netIDOwnership = new ConcurrentStack<(int, OwnershipCheck)>();
        public ConcurrentQueue<(int,int)> PingReplyBuffer = new ConcurrentQueue<(int,int)>();
        private static ConcurrentQueue<Packet> echoReplyBuffer = new ConcurrentQueue<Packet>();
        private IPEndPoint publicEndpoint { get; set; }
        private IPEndPoint rendezvousEndPoint { get; set; }
        private IPEndPoint holePunchingEndpoint { get; set; }
        public IPEndPoint ServerEndpoint { get; private set; }

        public Boolean pingReplyFlag { get; set; }
        public float pingingTime;
        public int rendezvousServerLatency;
        
        delegate void PacketHandler(Packet packet, IPEndPoint sender);

        private Dictionary<PacketType, PacketHandler> packetHandlers;

        


        public NetworkClient(Transport transport)
        {
           
            if (instance!=null)
            {
                Debug.LogError("Instance of NetworkClient is NOT NULL WTF");
            }
            
            
            instance = this;

            this.transport = transport;
            this.transport.networkClient = this;
            
            //this.expectedReplyFromClientID = -1;
            rendezvousEndPoint = new IPEndPoint(IPAddress.Any, 0000);
            
            packetHandlers = new Dictionary<PacketType, PacketHandler>
            {
                { PacketType.Connect, HandleConnectPacket },
                { PacketType.Disconnect, HandleDisconnectPacket },
                { PacketType.Ping, HandlePingPacket },
                { PacketType.PingReply, HandlePingReplyPacket },
                { PacketType.ClientList, HandleClientListPacket },
                { PacketType.ActorList, (packet, sender) => { Task.Run(() => HandleActorListPacket(packet)); } },
                { PacketType.Actor,  (packet, sender) => { Task.Run(() => HandleActorPacket(packet,sender)); }  },
                { PacketType.DestroyActor, HandleDestroyActorPacket },
                { PacketType.RPC, HandleRPCPacket },
                { PacketType.SyncVar, HandleSyncVarPacket },
                { PacketType.TransferOwnerShip, HandleTransferOwnerShipPacket },
                { PacketType.CheckOwnership, HandleCheckOwnershipPacket },
                { PacketType.CheckOwnershipReply, HandleCheckOwnershipReplyPacket },
                { PacketType.ActorRequest, HandleActorRequestPacket },
                { PacketType.EchoReply, HandleEchoReplyPacket }
            };
            

        }

        public PlayerStatus playerStatus { get; private set; }


        public bool ClientExists(int clientID)
        {
            Client client;
            clientList.TryGetValue(clientID, out client);
            return client != null;
        }

        #region Ping

        public float pingTC;


        

        
        /// <summary>
        /// Invoked each second, performs RTT calculation to connected clients and heartbeat.
        /// </summary>
        /// <returns></returns>
        public void ConnectionMonitoring()
        {
            
            if (isRendezvous)
            {
                SendEcho();
            }
            
            while (PingReplyBuffer.Count > 0)
            {
                Client client;
                (int, int) senderID_Delay;
                if (PingReplyBuffer.TryDequeue(out senderID_Delay))
                {
                    clientList.TryGetValue(senderID_Delay.Item1, out client);

                    if (client == null)
                    {
                        return;
                    }

                    int senderID = senderID_Delay.Item1;
                    
                    client.ping = senderID_Delay.Item2;
                    client.failedPingReplies = 0;
                    client.pingReplyReceived = true;
            
                    clientList[senderID] = client; //todo how "important" would it be to use a tryAndUpdate here instead?

                }
            }

            foreach (var v in clientList)
            {
                if (v.Key != clientID)
                {

                    Client client = clientList[v.Key];
                    if (client.pingReplyReceived)
                    {
                        client.pingReplyReceived = false;
                    }
                    else
                    {
                        client.failedPingReplies++;
                        /* client.ping = 999; */ // Packet loss could occur, ignore 999 ping.
                        
                        /* If 4 failed ping replies have been received, it most likely isn't any packet loss. */
                        if (client.failedPingReplies >= 4) 
                        {
                            /* Now could set 999 ping, as connection drop has most likely occured. */
                            client.ping = 999;
                            
                            /* Give 4 more tries, then remove the client.  */
                            if (client.failedPingReplies >= 24)
                            {
                                removeClient(v.Key);
                            }
                        }
                    }
                }

            }

           

        }

        public void sendPings()
        {
            foreach (var v in clientList)
            {
                if (v.Key != clientID)
                {
                    byte[] data = Serializer.SerializeLatencyDiagnostic(new LatencyDiagnostic(DateTime.UtcNow));
                    Packet packet = new Packet(PacketType.Ping, v.Key, clientID, data.Length, data);
                    SendPacket(packet, ResolveClientID(v.Key), TransportChannel.Unreliable);
                }
            }
        }

       /* public async Task<(int, double)> PingAsync(int senderID, int receiverID)
        {
            byte[] seqNumber = BitConverter.GetBytes(0); // Todo do this with enum aswell, important that the sequence number i zero here, otherwise it will be conflicts when using pings via button as it starts with 1 and upwards. 
            Packet packet = new Packet(PacketType.Ping, receiverID, senderID, seqNumber.Length, seqNumber);
            SendPacket(packet, GetEndpoint(receiverID), TransportChannel.Unreliable);

            double delay = await PingReplyTime();
            return (receiverID, delay);
        }
        */


        /*public (int,double) Ping(int senderID, int receiverID)
        {
            return PingAsync(senderID, receiverID).GetAwaiter().GetResult(); /* Force synchronous waiting */
        //}
    


        public async Task<double> PingReplyTime(int pingClientID)
        {
            float elapsedTime = 0;
            Stopwatch replyStopwatch = new Stopwatch();
            elapsedTime = 0; /* Reset elapsedTime for each handled or dropped replies. */
            replyStopwatch.Start(); /* Timestamp */
            Client client;
            int totalDelay = 999; 
            
            if (!clientList.TryGetValue(pingClientID, out client))
            {
                Debug.LogError("Trying to ping nonexisting client");
                return totalDelay;
            }

            client.pingReplyFlag = false;
            
            
            
            while (elapsedTime < totalDelay)
            {
                
                elapsedTime = (float)replyStopwatch.Elapsed.TotalMilliseconds;

                if (client.pingReplyFlag)
                {
                    break;
                }

            }

            if (elapsedTime >= totalDelay)
            {
                return totalDelay;
            }

            /* Otherwise ping was sent and received successfully. */

            replyStopwatch.Reset();
            return elapsedTime;
        }

        #endregion
        

        #region Client

        /// <summary>
        /// Does exactly what it says.
        /// </summary>
        /// <returns></returns>
        private IPEndPoint GetLocalEndpoint()
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            IPAddress myIP = localIPs.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (myIP != null)
                return new IPEndPoint(myIP, transport.port);

            return null;
        }

        private void GetPublicEndpoint()
        {
            ServerEndpoint = new IPEndPoint(IPAddress.Parse(NetworkManager.instance.holePunchServerAddress),
                NetworkManager.instance.holePunchServerPort);
            byte[] serializedLocalEndpoint = Serializer.SerializeIPEndPoint(privateEndpoint);
            Packet packet = new Packet(PacketType.PublicEndpoint, -1, -1, serializedLocalEndpoint.Length,
                serializedLocalEndpoint);
            transport.SendPacket(packet, ServerEndpoint, TransportChannel.Unreliable);
        }

        public void HandlePublicEndpointPacket(Packet packet)
        {
            IPEndPoint publicEP = Serializer.DeserializeIPEndPoint(packet.data);
            publicEndpoint = publicEP;

            if (rendezvousEndPoint.Address.ToString() == privateEndpoint.Address.ToString())
            {
                rendezvousEndPoint.Address = IPAddress.Loopback;
            }

            ReceivePublicEndPoint();
        }

        public void ReceivePublicEndPoint()
        {
            connectionStatus.TryAdd(publicEndpoint, ConnectionStatus.Mine);
            clientList.TryAdd(clientID, new Client(publicEndpoint, privateEndpoint));


            if (isRendezvous)
            {
                playerStatus = PlayerStatus.Connected;

                SetRendezvousInLobby();

                //NetworkManager.instance.RequestInstantiateLocalActor(0, 0, -1, Vector3.zero, Vector3.zero);
                Actor playerActor = new Actor()
                {
                    clientID = clientID,
                    netID = 0,
                    prefabID = -1,
                    position = NetworkManager.instance.GetRandomSpawnPoint(),
                };


                NetworkManager.instance.InstantiateGlobalActor(playerActor);
            }
            else
            {
                playerStatus = PlayerStatus.EstablishingConnection;

                SendReadyForConnect();
            }
        }

        /// <summary>
        /// Resolves clientID into its IpEndPoint. It should be predetermined that this client is aware of the other client.
        /// </summary>
        /// <param name="clientID"></param>
        /// <returns> IpEndPoint, Null if the there is no knowledge of this a client with this id. </returns>
        public IPEndPoint ResolveClientID(int clientID)
        {
            //Make into one method
            return GetEndpoint(clientID);
        }



        /// <summary>
        /// Start hosting a session
        /// </summary>
        public void Host()
        {
            playerStatus = PlayerStatus.Connecting;
            clientID = 0;
            lastUsedClientID = 0;
            isRendezvous = true;
            publicEndpoint = new IPEndPoint(IPAddress.Any, 0);
            privateEndpoint = GetLocalEndpoint();
            Task.Run(() => RendezvousTrackEchoReplies(ServerEndpoint)); // todo add echoes check together with ping check.
            GetPublicEndpoint();
        }

        public void Connect()
        {
            lastUsedClientID = 0;
            playerStatus = PlayerStatus.EstablishingConnection;
            /* Save the client's local ip address */
            publicEndpoint = new IPEndPoint(IPAddress.Any, 0);
            privateEndpoint = GetLocalEndpoint();
            GetPublicEndpoint();
            clientID = -1;
            isRendezvous = false;
        }

        private void StartConnectProcedure()
        {
            byte[] serializedEPs = Serializer.SerializeConnectPacket(publicEndpoint, privateEndpoint, -1);

            Packet packet = new Packet(PacketType.Connect, -1, -1, serializedEPs.Length, serializedEPs);
            SendPacket(packet, rendezvousEndPoint, TransportChannel.Unreliable);

            /* Debug */
            if (NetworkManager.instance.ProcedureDebug)
                Debug.Log("Client[" + this.clientID + "]" + " Connecting to rendezvous client... Everything is set up!");
        }

        /// <summary>
        /// Disconnect from session
        /// </summary>
        public async Task Disconnect()
        {
            playerStatus = PlayerStatus.Disconnecting;

            foreach (KeyValuePair<int, Client> client in clientList)
            {
                if (client.Key != clientID) /* Client should not send a disconnect packet to itself.  */
                {
                    Packet packet = new Packet(PacketType.Disconnect, client.Key, clientID, 0, new byte[0]);
                    packet.channel = TransportChannel.Unreliable;

                    for (int i = 0; i < 10; i++)
                    {
                        (transport as UDPTransport).SendAsyncPacket(packet, GetEndpoint(client.Key));
                    }
                }
            }


            transport.StopTransport(); /* Signals udpClient thread to close. */
            await WaitForTransportClose();
            
            connectionStatus.Clear();
            netIDOwnership.Clear();
            clientList.Clear();
            echoReplyBuffer.Clear();
            clientList = null;
            netIDOwnership = null;
            clientList = null;
            instance = null;

        }

        public async Task WaitForTransportClose()
        {
            while (transport.isRunning) /* Polling until socket reaches timeout and closes. */
            {
                //await Task.Delay(10);
            }
            Debug.Log("Transport is now closed");
        }


        /// <summary>
        /// Used to transmit messages to the transport layer
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="endPoint"></param>
        public void SendPacket(Packet packet, IPEndPoint endPoint, TransportChannel channel)
        {
            transport.SendPacket(packet, endPoint, channel);
        }


        /// <summary>
        /// Receives packets from the transport layer
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        public void HandlePacket(Packet packet, IPEndPoint sender)
        {
            //Hopefully not needed anymore

        
            
            /* If packet is not intended for me and I am not rendezvous client.*/
            if (clientID != packet.receiverID && !isRendezvous && clientID != -1)
            {
                if (NetworkManager.instance.PingDebug) Debug.LogWarning("Discarded packet, not intended for me");
                Debug.Log("clientID: " + clientID + "| packets receieverID: " + packet.receiverID);
                return;
            }

            /* If packet is not intended for me and I am not rendezvous client.*/
            if (clientID != packet.receiverID && isRendezvous)
            {
                if (packet.senderID != -1 && packet.senderID != serverID)
                {
                    if (NetworkManager.instance.PingDebug)
                        Debug.LogWarning("[Rendezvous] Discarded packet, not intended for me" + "packet senderID: " + packet.senderID);

                    return;
                }
            }

            /* Debug check */
            if (NetworkManager.instance.PacketDebug)
                Debug.Log("Client[" + this.clientID + "]" + "Handling packetType: " + packet.packetType  // todo it is wrong here, Client[0] handling packettype: pingreplysent from client: 0 on 3.67.29.230:5000
                          + "sent from client: " + packet.senderID + " on " + sender);

            if (packetHandlers.TryGetValue(packet.packetType, out var handler))
            {
                handler(packet, sender);

         
            }
            else
            {
            {
                Debug.LogError("Packet type not recognized. PacketType was "+packet.packetType.ToString());
                // Handle unexpected packet types here
            }
               
              
            }
        }

        #endregion

        public async void HandleEchoReplyPacket(Packet packet, IPEndPoint sender)
        {
            echoReplyBuffer.Enqueue(packet);
        }
        
        public void SendEcho()
        {
            LatencyDiagnostic echoTimeStamp = new LatencyDiagnostic(DateTime.UtcNow);
            byte[] data = Serializer.SerializeLatencyDiagnostic(echoTimeStamp);
            Packet echoPacket = new Packet(PacketType.Echo, serverID, clientID, data.Length, data);
            SendPacket(echoPacket, ServerEndpoint, TransportChannel.Unreliable);
        }
        
      
        public async Task RendezvousTrackEchoReplies(IPEndPoint receiver)
        {   
            /* Extra step to determine rendezvous client's RTT to the server. This delay is then added with, the client viewing the lobby, delay to show what QoS can be expected for the specific lobby. */
            
            /* Determining the peer to peer networks overall QoS would be to complex considering everyone experience different delays to each other. This calculation was chosen as initial connection goes through
             the rendezvous and server anyways and could let people knowing what servers they should join and which ones they should not join. */ 
            // todo got an idea of switching of rendezvous responsibility if the current host gets bad connection
            
            int counter = 0;
            
            while (isRendezvous)
            {
                while (echoReplyBuffer.Count > 0)
                {
                    Packet echo;
                    echoReplyBuffer.TryDequeue(out echo);

                    LatencyDiagnostic echoTimeStamp = Serializer.DeserializeLatencyDiagnostic(echo.data);

                    //Debug.Log("Received echo reply");
                    int latencyMS;
                    if (LatencyDiagnostic.IsWithinSecond(echoTimeStamp.sentTimeStamp, out latencyMS))
                    {
                        //Debug.Log("Received echo reply with latency: " + latencyMS);
                        NetworkManager.instance.rendezvousServerRTT = latencyMS;
                    }
                    else
                    {
                        NetworkManager.instance.rendezvousServerRTT = 999;
                    }
                }
            }


        }
        #region ConnectingClient

        public IPEndPoint GetEndpoint(int clientID)
        {

            if (clientID == serverID)
            {
                return ServerEndpoint;
            }
            
            Client client;


            if (!clientList.TryGetValue(clientID, out client))
            {
                //Debug.LogError("Client was not found in clientList");
                return null;
            }

            if (client==null)
            {
                Debug.LogError("Client was null!");
                return null;
            }
            
            
            if (client.publicEP==null)
            {
                Debug.LogError("Public EP of client did not exist!");
                return null;
            }
            
            ConnectionStatus _connectionStatus;
            if (!connectionStatus.TryGetValue(client.publicEP,out _connectionStatus))
            {
                Debug.LogError("No connection status to client.");
                return null;
            }
            
            
            switch (_connectionStatus)
            {
                case ConnectionStatus.Local:
                    return new IPEndPoint(IPAddress.Loopback, client.privateEP.Port);
                case ConnectionStatus.Mine:
                    return new IPEndPoint(IPAddress.Loopback, client.privateEP.Port);
                case ConnectionStatus.None:
                    return client.publicEP;
                case ConnectionStatus.Private:
                    return client.privateEP;
                case ConnectionStatus.Public_Direct:
                    return client.publicEP;
                case ConnectionStatus.Public_Relay:
                    return client.publicEP;
            }
            
            Debug.LogError("Invalid Connection status to client "+clientID);
            return null;
            /*
            if (!clientList.ContainsKey(clientID))
            {
                return null;
            }
            if (clientList[clientID].publicEP.Address.ToString() == publicEndpoint.Address.ToString())
            {
                if (clientList[clientID].privateEP.Address.ToString() == privateEndpoint.Address.ToString())
                {
                    return new IPEndPoint(IPAddress.Loopback, clientList[clientID].privateEP.Port);
                }
                else
                {
                    return clientList[clientID].privateEP;
                }
            }
            return clientList[clientID].publicEP;
            */
        }

        public bool IsConnectedToEveryone()
        {
            bool temp = true;

            foreach (var kvp in clientList)
            {
                if (kvp.Value.publicEP == publicEndpoint || !connectionStatus.ContainsKey(kvp.Value.publicEP))
                {
                    Debug.Log("Is this the reason?");
                    continue;
                }

                if (connectionStatus[kvp.Value.publicEP] == ConnectionStatus.None)
                {
                    temp = false;
                }
            }

            return temp;
        }

        /// <summary>
        /// Run when receiving a connect packet. If connecting client hasn't received ID, ID will be given. If an ID is
        /// given, this method will request to instantiate a local actor for the new client.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        ///
        private void HandleConnectPacket(Packet packet, IPEndPoint sender)
        {
            (IPEndPoint, IPEndPoint, int) publicAndPrivateEPAndClientID =
                Serializer.DeserializeConnectPacket(packet.data);

            Debug.Log("Handle Connect from : PUBLIC :" + publicAndPrivateEPAndClientID.Item1 + " PRIVATE : " +
                      publicAndPrivateEPAndClientID.Item2);

            /* Invoke on rendezvous client. */
            if (packet.senderID == -1)
            {
                AssignNewClient(publicAndPrivateEPAndClientID.Item1, publicAndPrivateEPAndClientID.Item2);
            }
            /* Invoke for client that received connect packet. (Used only if multiple clients share same IP)*/
            else if (!isRendezvous)
            {
                AddNewLocalClient(publicAndPrivateEPAndClientID.Item3, publicAndPrivateEPAndClientID.Item1,
                    publicAndPrivateEPAndClientID.Item2);
            }
        }

        /// <summary>
        /// Receives the current actor list from the rendezvous client.
        /// Runs on the connecting player and requests to instantiate all the received actors.
        /// </summary>
        /// <param name="packet">The packet that contains the actor list.</param>
        private async Task HandleActorListPacket(Packet packet)
        {
            byte[] data = packet.data;

            /* I don't think it is necessary to create an instance first? */
            // List<Actor> actorList = new List<Actor>();

            List<Actor> actorList = (List<Actor>)Serializer.DeserializeActorList(data);

            /* Instantiate the received actors */
            foreach (var client in actorList)
            {
                NetworkManager.instance.RequestInstantiateLocalActor(client.clientID, client.netID, client.prefabID,
                    client.position, client.rotation);
                Debug.Log("Client[" + this.clientID + "]" + "Instantiating Local Actor with owner: " + client.clientID);
            }


            Actor playerActor = new Actor()
            {
                clientID = clientID,
                netID = 0,
                prefabID = -1,
                position = NetworkManager.instance.GetRandomSpawnPoint(),
            };


            //Wait for connected to everybody.
            while (!IsConnectedToEveryone())
            {
                Debug.Log("Wait for connect");
            }

            //TODO: Maybe implement some kind of queue if the connection between two clients hasnt been established.

            /* This method is responsible for instantiating the player actor of the new client on the network.
            The player actor will be automatically transmitted and instantiated on every connected client.
            If the receiver has not yet added this client to its client list, it will do so upon receiving the player actor
            Is ONLY place where the new client's player actor is called to be instantiated. */
            NetworkManager.instance.InstantiateGlobalActor(playerActor);

            playerStatus = PlayerStatus.Connected;
            Debug.Log("Connected to everyone");

            /* Debug */
            if (NetworkManager.instance.AdvancedDebug)
                Debug.Log("Client[" + this.clientID + "]" + " Received actor list with: " + actorList.Count +
                          " actors");
        }

        /// <summary>
        /// Receives the client list from the rendezvous client. This is where the unique clientID is set for the connecting client.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        private void HandleClientListPacket(Packet packet, IPEndPoint sender)
        {
            Dictionary<int, Client> clients = Serializer.DeserializeClientDictionary(packet.data);

            clientList = new ConcurrentDictionary<int, Client>(clients);

            /* Assign clientID */
            clientID = clientList.Keys.ToList().Max();

            foreach (var kvp in clientList)
            {
                if (kvp.Key == clientID)
                {
                    continue;
                }

                if (connectionStatus.ContainsKey(kvp.Value.publicEP))
                {
                    if (connectionStatus[kvp.Value.publicEP] != ConnectionStatus.None)
                    {
                        continue;
                    }
                }

                ConnectToEndPoint(kvp.Value.publicEP, kvp.Value.privateEP);
            }


            /* Debug */
            if (NetworkManager.instance.AdvancedDebug)
                Debug.Log("Client[" + this.clientID + "]" + " Received client list with: " + clientList.Count +
                          " clients. Assigning clientID: " + clientID);
        }

        #endregion

        #region ActorManagement

        /// <summary>
        /// This method should ONLY be called from the NetworkManager's "InstantiatePlayer" & "InstantiateActor".
        /// Sends a new actor to all connected clients.
        /// </summary>
        /// <param name="actor"></param>
        public void SendActor(Actor actor)
        {
            byte[] serializedActor = Serializer.SerializeActor(actor);

            /* Send to every connected client. */
            foreach (var kvp in clientList)
            {
                if (kvp.Key == clientID)
                    continue;

                Packet packet = new Packet(PacketType.Actor, kvp.Key, clientID, serializedActor.Length,
                    serializedActor);
                //Debug.Log("SendActor() " + "to: " + packet.receiverID);
                SendPacket(packet, GetEndpoint(kvp.Key), TransportChannel.Reliable);
            }
        }
        
        public void SendActor(Actor actor, int receiverID)
        {
            byte[] serializedActor = Serializer.SerializeActor(actor);

            Packet packet = new Packet(PacketType.Actor, receiverID, clientID, serializedActor.Length,
                serializedActor);
            Debug.Log("SendActor() " + "to: " + packet.receiverID);
            SendPacket(packet, GetEndpoint(receiverID), TransportChannel.Reliable);
            
        }

        /// <summary>
        /// Receives a new actor. Will forward to InstantiateLocalActor. NOTE that this method will be used
        /// to add any new clients to non rendezvous clients. See the comments in the code
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        private void HandleActorPacket(Packet packet, IPEndPoint sender)
        {
            /* Debug */
            if (NetworkManager.instance.PacketDebug) Debug.Log("Received actor was " + packet.data.Length + " bytes");

            Actor newActor = Serializer.DeserializeActor(packet.data);


            int counter = 0;
            
            /* Is this still necessary? */
            while (!clientList.ContainsKey(packet.senderID))
            {
                if (counter>=5000)
                {
                    break;
                }
                counter++;
                Task.Delay(1);
            }

            
            
            /* Instantiate it locally */
            NetworkManager.instance.RequestInstantiateLocalActor(newActor.clientID, newActor.netID, newActor.prefabID,
                newActor.position, newActor.rotation);

            //NetworkManager.instance.InstantiateLocalActor(newActor);
        }

        public void SendDestroyActor(Actor actor)
        {
            byte[] serializedActor = Serializer.SerializeDestroyActor(actor);

            /* Send to every connected client. */
            foreach (var kvp in clientList)
            {
                if (kvp.Key == clientID)
                    continue;

                Packet packet = new Packet(PacketType.DestroyActor, kvp.Key, clientID, serializedActor.Length,
                    serializedActor);
                //Debug.Log("SendingDestroyActor() " + "to: " + packet.receiverID);
                SendPacket(packet, GetEndpoint(kvp.Key), TransportChannel.Reliable);
            }
        }

        private void HandleDestroyActorPacket(Packet packet, IPEndPoint sender)
        {
            /* Debug */
            if (NetworkManager.instance.PacketDebug) Debug.Log("Received destroy actor");

            Actor receivedActor = Serializer.DeserializeDestroyActor(packet.data);

            /* Destroy it locally */
            NetworkManager.instance.RequestDestroyLocalActor(receivedActor);
        }

        /// <summary>
        ///  Instantiates player actor and adds to client list.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="newClient"></param>
        private void AddNewLocalClient(int clientID, IPEndPoint publicEP, IPEndPoint privateEP)
        {
            clientList.TryAdd(clientID, new Client(publicEP, privateEP));

            ConnectToEndPoint(publicEP, privateEP);

            //NetworkManager.instance.RequestInstantiateLocalActor(clientID,0,-1);

            /* Debug */
            if (NetworkManager.instance.AdvancedDebug)
                Debug.Log("Client[" + this.clientID + "]" + " Added a new local client with clientID: " + clientID);
        }

        public void SendTransferOwnerShip(NetworkIdentity networkIdentity, int newOwner, int netID)
        {
            byte[] serializedOwnerShip =
                Serializer.SerializeObjectParams(new object[] { networkIdentity, newOwner, netID });

            /* Send to every connected client. */
            foreach (var kvp in clientList)
            {
                if (kvp.Key == clientID)
                    continue;

                Packet packet = new Packet(PacketType.TransferOwnerShip, kvp.Key, clientID, serializedOwnerShip.Length,
                    serializedOwnerShip);
                Debug.Log("SendTransferOwnerShip() " + kvp.Key);
                SendPacket(packet, GetEndpoint(kvp.Key), TransportChannel.Reliable);
            }
        }

        public async Task<OwnershipCheck> SendCheckOwnershipAsync(int receiverID, int netID)
        {
            byte[] serializedNetID = BitConverter.GetBytes(netID);
            Packet packet = new Packet(PacketType.CheckOwnership, receiverID, clientID, serializedNetID.Length, serializedNetID);
            Debug.Log("SendCheckOwnerShipAsync() " + "to: " + packet.receiverID);
            SendPacket(packet, GetEndpoint(receiverID), TransportChannel.Reliable);

            return await WaitForOwnershipReply(netID);
            
        }

        public async Task<OwnershipCheck> WaitForOwnershipReply(int netID)
        {
            
            int timeout = 0;
            int delay = 10;
            while (true)
            {
                await Task.Delay(delay);
                timeout += delay;
                
                (int, OwnershipCheck) temp;
                netIDOwnership.TryPeek(out temp);

                if (temp.Item1 == netID)
                {
                    return OwnershipCheck.IsOwner;
                }

                if (delay >= 1000)
                {  
                    return OwnershipCheck.CheckTimedOut;
                }
            }

        }
        
        #endregion

        #region RendezvousClient

        /// <summary>
        /// Runs on the rendezvous client to assign a client ID to a new client.
        /// Then, sends the client list and the actor list to the new client.
        /// This method should ONLY be used during the join phase.
        /// </summary>
        /// <param name="newClient"> The new client that is joining the network.</param>
        private void AssignNewClient(IPEndPoint publicEP, IPEndPoint privateEP)
        {
            //This is neccessary, because sometimes, the HP packet is received AFTER the other clients connect packet, if its local. 
            //This is very likely, since they have a much lower delay.

            if (!connectionStatus.ContainsKey(publicEP))
            {
                if (publicEP.Address.Equals(publicEndpoint.Address) && !NetworkManager.instance.forceRelay)
                {
                    if (privateEndpoint.Address.Equals(privateEP.Address))
                    {
                        SetConnectionStatus(publicEP, ConnectionStatus.Local);
                    }
                    else
                    {
                        SetConnectionStatus(publicEP, ConnectionStatus.Private);
                    }
                }
                else
                {
                    SetConnectionStatus(publicEP, ConnectionStatus.Public_Relay);
                }
            }


            int newClientID = ++lastUsedClientID;
            bool result = clientList.TryAdd(newClientID, new Client(publicEP, privateEP));


            if (!result)
            {
                Debug.LogError("Couldn't insert Client");
            }

            //Send Connect packet to all peers from the new client.
            byte[] serializedEPs = Serializer.SerializeConnectPacket(publicEP, privateEP, newClientID);

            foreach (var kvp in clientList)
            {
                if (kvp.Key == newClientID || kvp.Key == clientID)
                {
                    continue;
                }

                Packet connectPacket = new Packet(PacketType.Connect, kvp.Key, clientID, serializedEPs.Length,
                    serializedEPs);
                Debug.Log("AssignNewClient, sending connect packet " + "to: " + kvp.Key);
                SendPacket(connectPacket, GetEndpoint(kvp.Key), TransportChannel.Reliable);
            }


            SendClientList(GetEndpoint(newClientID), newClientID);

            /* Since the new client will now have a clientID, we need to send the actor list to that ID, or it will be discarded. */
            //SendActorList(GetEndpoint(newClientID), newClientID);

            NetworkManager.instance.EnqueueAction(() => {
                SendActorList(GetEndpoint(newClientID), newClientID);
            });
            
            /* Debug */
            if (NetworkManager.instance.ProcedureDebug)
                Debug.Log("[Rendezvous] Assigned clientID: " + publicEP + " for new client.");
        }

        /// <summary>
        /// Sends the client list to the connecting client.
        /// This method should ONLY be used during the join phase.
        /// </summary>
        /// <param name="endPoint"></param>
        private void SendClientList(IPEndPoint endPoint, int newClientID)
        {
            //byte[] serializedList = SerializeDictionary(clientList);
            byte[] serializedList = Serializer.SerializeClientDictionary(new Dictionary<int, Client>(clientList));

            Packet packet = new Packet(PacketType.ClientList, newClientID, clientID, serializedList.Length,
                serializedList);
            SendPacket(packet, endPoint, TransportChannel.Unreliable);

            /* Debug */
            if (NetworkManager.instance.AdvancedDebug)
                Debug.Log("[Rendezvous] Sending client list of: " + clientList.Count + " clients.");
        }

        /// <summary>
        /// Sends the actor list to the connecting client.
        /// This method should ONLY be used during the join phase.
        /// </summary>
        /// <param name="endPoint"></param>
        private void SendActorList(IPEndPoint endPoint, int newClientID)
        {
            List<Actor> list = new List<Actor>();
            foreach (var kvp in NetworkManager.instance.activeActorObjects)
            {
                kvp.Key.position = kvp.Value.transform.position;
                kvp.Key.rotation = kvp.Value.transform.eulerAngles;
                
                list.Add(kvp.Key);
            }

            byte[] serializedList = Serializer.SerializeActorList(list);
            Packet packet = new Packet(PacketType.ActorList, newClientID, clientID, serializedList.Length,
                serializedList);
            
            Debug.Log("SendActorList() sending to: " + packet.receiverID);
            SendPacket(packet, endPoint, TransportChannel.Reliable);

            /* Debug */
            if (NetworkManager.instance.AdvancedDebug)
                Debug.Log("[Rendezvous] Sending actor list of: " + NetworkManager.instance.activeActorObjects.Count +
                          " actors.");
        }

        #endregion

        #region PacketHandlers

        private void HandleDisconnectPacket(Packet packet,IPEndPoint receiver)
        {
            removeClient(packet.senderID);
        }

        private void removeClient(int clientID)
        {
            Client removedClient;
            
            
            if (clientList.TryRemove(clientID, out removedClient))
            {
                ConnectionStatus removed;
                connectionStatus.TryRemove(removedClient.publicEP,out removed);

                
                if ((transport as UDPTransport).clientRTT_RTTVAR.ContainsKey(clientID))
                {
                    (transport as UDPTransport).clientRTT_RTTVAR.Remove(clientID);
                }
                
                Debug.Log("Removed client " + clientID);
                NetworkManager.instance.DestroyAllLocalActorsOwnedByClient(clientID);

                if (!isRendezvous)
                {
                    DecideIfNewRendezvous();
                }

            }

        }

        private void HandlePingPacket(Packet packet, IPEndPoint sender) // got from correct client.
        {
            Packet packetReply = packet;
            packetReply.receiverID = packet.senderID;
            packetReply.senderID = clientID; // todo packet.receiverID was 1 which is not correct, it should have been 0.(!!!) solved this temporarily with clientID.
            packetReply.packetType = PacketType.PingReply;
            SendPacket(packetReply, GetEndpoint(packetReply.receiverID), TransportChannel.Unreliable);
        }

        private void HandlePingReplyPacket(Packet packet, IPEndPoint sender)
        {

            LatencyDiagnostic packetLatency = Serializer.DeserializeLatencyDiagnostic(packet.data);

            /*if (PingManager.instance.totalHandledPackets >= seqNumber && seqNumber != 0)
            {
                if (NetworkManager.instance.PingDebug)
                {
                    Debug.Log("Already handled packet, timed out before. seq number: " + seqNumber);
                }

                return;
            }
            */

            int latencyMS;
            if (LatencyDiagnostic.IsWithinSecond(packetLatency.sentTimeStamp, out latencyMS))
            {
                PingReplyBuffer.Enqueue((packet.senderID, latencyMS));
            }
        }

        private void HandleRPCPacket(Packet packet, IPEndPoint sender)
        {
            if (playerStatus != PlayerStatus.Connected) return;
            RPCRequest rpcRequest = Serializer.DeserializeRPC(packet.data);

            NetworkManager.instance.InvokeLocalRPC(rpcRequest);
        }

        private void HandleSyncVarPacket(Packet packet, IPEndPoint sender)
        {
            if (playerStatus != PlayerStatus.Connected)
            {
                Debug.LogWarning("Received syncVar from disconnected player");
                return;
            }

            SyncVarRequest syncVarRequest = Serializer.DeserializeSyncVar(packet.data);
            NetworkManager.instance.SetLocalSyncVars(syncVarRequest);
  
        }
        public void HandleTransferOwnerShipPacket(Packet packet,IPEndPoint receiver) 
        {
            object[] ownership = Serializer.DeserializeObjectParams(packet.data);

            NetworkIdentity networkIdentity = ownership[0] as NetworkIdentity;
            int newOwner = (int)ownership[1];
            int netID = (int)ownership[2];

            NetworkManager.instance.TransferLocalOwnerShip(networkIdentity, newOwner, netID);
        }

        public void HandleCheckOwnershipPacket(Packet packet,IPEndPoint receiver)
        {
            int netID = BitConverter.ToInt32(packet.data);
            
            /* Check if you are owner of this NetworkIdentity */
            OwnershipCheck ownershipStatus;
            if (NetworkManager.instance.networkIdentities.ContainsKey((clientID,netID)))
            {
                ownershipStatus = OwnershipCheck.IsOwner;

            }
            else if (NetworkManager.instance.networkIdentities.Keys.Any(key => key.Item2 == netID))
            {

                ownershipStatus = OwnershipCheck.OwnedByOthers;
            }
            else
            {
                ownershipStatus = OwnershipCheck.NonExistent;
            }

            (int, OwnershipCheck) ownershipStatusData = (netID, ownershipStatus);
            byte[] serializedownershipStatusData = Serializer.SerializeNetIDOwnership(ownershipStatusData);
            
            Packet packetReply = new Packet(PacketType.CheckOwnershipReply, packet.senderID, clientID, serializedownershipStatusData.Length, serializedownershipStatusData);
            SendPacket(packetReply, GetEndpoint(packet.senderID), TransportChannel.Reliable);
        }


        public void HandleActorRequestPacket(Packet packet,IPEndPoint receiver)
        {
            int netID = BitConverter.ToInt32(packet.data);
            
            foreach (var obj in NetworkManager.instance.activeActorObjects)
            {
                if (obj.Key.netID == netID) 
                {
                    //Debug.Log("Handled actor request packet, sending actor!");
                    SendActor(obj.Key, packet.senderID);
                    return;
                }
            }
            Debug.LogWarning("Actor Request but client does not own actor with specified netID.");
        }

        public void HandleCheckOwnershipReplyPacket(Packet packet,IPEndPoint receiver)
        {
            byte[] serializedownershipStatusData = packet.data;

            (int, OwnershipCheck)  ownershipStatusData = Serializer.DeserializeNetIDOwnership(serializedownershipStatusData);
            
            netIDOwnership.Push( (ownershipStatusData) );
        }

        #endregion

        #region RPC

        public void SendRPC(RPCRequest rpcRequest, TransportChannel channel, int targetClientID)
        {
            byte[] bytes = Serializer.SerializeRPC(rpcRequest);


            if (targetClientID != -1)
            {
                Packet packet = new Packet(PacketType.RPC, targetClientID, clientID, bytes.Length, bytes);
                SendPacket(packet, GetEndpoint(targetClientID), channel);
                return;
            }


            if (clientList.Count == 0)
            {
                Debug.LogError("Client List Error");
            }


            foreach (var kvp in clientList)
            {
                if (kvp.Key != clientID)
                {
                    Packet packet = new Packet(PacketType.RPC, kvp.Key, clientID, bytes.Length, bytes);
                    SendPacket(packet, GetEndpoint(kvp.Key), channel);
                }
            }
        }

        #endregion

        #region SyncVar

        public void SendSyncVar(SyncVarRequest syncVarRequest, TransportChannel channel)
        {
            byte[] bytes = Serializer.SerializeSyncVar(syncVarRequest);

            foreach (var kvp in clientList)
            {
                if (kvp.Key != clientID)
                {
                    Packet packet = new Packet(PacketType.SyncVar, kvp.Key, clientID, bytes.Length, bytes);
                    SendPacket(packet, GetEndpoint(kvp.Key), channel);
                }
            }
        }
        
        public void SendTargetSyncVar(int clientID,SyncVarRequest syncVarRequest, TransportChannel channel)
        {
            byte[] bytes = Serializer.SerializeSyncVar(syncVarRequest);

            Packet packet = new Packet(PacketType.SyncVar, clientID, this.clientID, bytes.Length, bytes);
            SendPacket(packet, GetEndpoint(clientID), channel);
        }

        #endregion

        #region EstablishConnection

        private void DecideIfNewRendezvous()
        {
            if (clientList.Keys.Min() == clientID)
            {
                SetRendezvousInLobby();
                Task.Run(() => RendezvousTrackEchoReplies(ServerEndpoint));
            }
        }

        public void SendReadyForConnect()
        {
            byte[] serializedLocalEP =
                Serializer.SerializeIpEndpointWithString(privateEndpoint, NetworkManager.instance.lobbyName);
            Packet packet = new Packet(PacketType.HolePunch, -1, clientID, serializedLocalEP.Length, serializedLocalEP);
            transport.SendPacket(packet,
                new IPEndPoint(IPAddress.Parse(NetworkManager.instance.holePunchServerAddress),
                    NetworkManager.instance.holePunchServerPort), TransportChannel.Unreliable);
        }

        public void SetRendezvousInLobby() 
        {
            
            if (!isRendezvous)
            {
                lastUsedClientID = clientList.Keys.Max()+2;
            }

            isRendezvous = true;
            byte[] serializedLocalEP =
                Serializer.SerializeIpEndpointWithString(privateEndpoint, NetworkManager.instance.lobbyName);
          
            Packet packet = new Packet(PacketType.SetRendezvousInLobby, serverID, clientID, serializedLocalEP.Length,
                serializedLocalEP);
            transport.SendPacket(packet, ServerEndpoint, TransportChannel.Unreliable); // todo this might not be suitable for unreliable?

            LobbyInformation lobbyInformation = NetworkManager.instance.GetLocalLobbyInformation();
      
            LobbyManager.instance.SetLobbyInformation(lobbyInformation);

        }

        
        
        public void HandleHolePunchPacket(Packet packet)
        {
            (IPEndPoint, IPEndPoint) publicAndLocalEP = Serializer.DeserializePublicAndPrivateEP(packet.data);

            //Is not sent from server
            if (packet.senderID != 1337)
            {
                if (NetworkManager.instance.forceRelay)
                {
                    return;
                }


                if (connectionStatus.ContainsKey(publicAndLocalEP.Item1))
                {
                    return;
                }

                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Received Hole Punch Packet from " + packet.senderID);
                }

                SetConnectionStatus(publicAndLocalEP.Item1, ConnectionStatus.Public_Direct);
                return;
            }

            //This is for the rendezvous....
            //publicAndLocalEP = Serializer.DeserializePublicAndPrivateEP(packet.data);


            holePunchingEndpoint = publicAndLocalEP.Item1;

            if (!NetworkManager.instance.forceRelay)
            {
                //If both clients are behind the same NAT
                if (holePunchingEndpoint.Address.ToString() == publicEndpoint.Address.ToString() &&
                    !NetworkManager.instance.forceRelay)
                {
                    if (publicAndLocalEP.Item2.Address.ToString() == privateEndpoint.Address.ToString())
                    {
                        rendezvousEndPoint = new IPEndPoint(IPAddress.Loopback, publicAndLocalEP.Item2.Port);
                        SetConnectionStatus(holePunchingEndpoint, ConnectionStatus.Local);
                    }
                    else
                    {
                        rendezvousEndPoint = publicAndLocalEP.Item2;
                        SetConnectionStatus(holePunchingEndpoint, ConnectionStatus.Private);
                    }

                    if (!isRendezvous && playerStatus == PlayerStatus.EstablishingConnection)
                    {
                        StartConnectProcedure();
                    }
                    
                    return;
                }
            }


            //hasConnectionToClient.Add(holePunchingEndpoint,false);
            Task hpThread = new Task(() => performHolePunching(holePunchingEndpoint));
            hpThread.Start();
            if (NetworkManager.instance.ProcedureDebug)
            {
                Debug.Log("Started hp thread");
            }
        }

        private async Task performHolePunching(IPEndPoint ipEndPoint)
        {
            bool success = false;

            byte[] eps = Serializer.SerializePublicAndPrivateEP(publicEndpoint, privateEndpoint);


            if (!NetworkManager.instance.forceRelay)
            {
                for (int i = 0; i < 20; i++)
                {
                    Packet packet = new Packet(PacketType.HolePunch, -1, -1, eps.Length, eps);
                    SendPacket(packet, ipEndPoint, TransportChannel.Unreliable);

                    if (connectionStatus.ContainsKey(ipEndPoint))
                    {
                        if (connectionStatus[ipEndPoint] != ConnectionStatus.None)
                        {
                            success = true;
                            break;
                        }
                    }

                    Thread.Sleep(100);
                }
            }


            if (success)
            {
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Hole Punching Successful with EP " + ipEndPoint);
                }
                SetConnectionStatus(ipEndPoint, ConnectionStatus.Public_Direct);
            }
            else
            {
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Hole Punching Failed with EP " + ipEndPoint + "! USE RELAY");
                }
               
                SetConnectionStatus(ipEndPoint, ConnectionStatus.Public_Relay);
            }

            HolePunchingComplete(ipEndPoint);
        }

        private void HolePunchingComplete(IPEndPoint ipEndPoint)
        {
            if (playerStatus == PlayerStatus.EstablishingConnection)
            {
                playerStatus = PlayerStatus.Connecting;
                rendezvousEndPoint = ipEndPoint;

                StartConnectProcedure();
            }
        }

        private void ConnectToEndPoint(IPEndPoint publicEP, IPEndPoint privateEP)
        {
            if (publicEP.Address.ToString() == publicEndpoint.Address.ToString())
            {
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Is Local Client! Connecting should already work");
                }


                if (privateEP.Address.Equals(privateEndpoint.Address))
                {
                    SetConnectionStatus(publicEP, ConnectionStatus.Local);
                }
                else
                {
                    SetConnectionStatus(publicEP, ConnectionStatus.Private);
                }
            }
            else
            {
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Is Remote Client. Need to perform hole punching");
                }

                SetConnectionStatus(publicEP, ConnectionStatus.None);


                Thread hpThread = new Thread(() => performHolePunching(publicEP));
                hpThread.Start();
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Started hp thread");
                }
            }
        }

        private void SetConnectionStatus(IPEndPoint publicEP, ConnectionStatus conStat)
        {
            if (NetworkManager.instance.ProcedureDebug)
            {
                Debug.Log("Setting Connection status with " + publicEP.ToString() + " to " + conStat);
            }

            if (connectionStatus.ContainsKey(publicEP))
            {
                connectionStatus[publicEP] = conStat;
            }
            else
            {
                connectionStatus.TryAdd(publicEP, conStat);
            }
        }

        #endregion
    }


}