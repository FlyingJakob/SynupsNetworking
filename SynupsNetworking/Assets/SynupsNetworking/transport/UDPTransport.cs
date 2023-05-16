using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SynupsNetworking.core;
using SynupsNetworking.core.Enums;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Random = System.Random;

namespace SynupsNetworking.transport
{
    public class UDPTransport : Transport
    {
        private IPEndPoint receivedEndPoint;
        private UdpClient udpClient;
        private Thread receiveThread;
        private Thread handleSendBufferThread;
        public bool enablePacketCoalescing;
        
        /* If fields are not used, or empty. */
        private const int DEFAULT_PACKET_TIMEOUT = 1000;
        private const int DEFAULT_MAXIMUM_RETRANSMISSIONS = 1000;
        
        [Header("Reliable Protocol")] 
        public int packetTimeout = DEFAULT_PACKET_TIMEOUT;
        private int maximumRetransmissions = DEFAULT_MAXIMUM_RETRANSMISSIONS;
        
        [Header("Adaptive throttling")] 
        public bool adaptiveThrottling = false;
        public int timeToEmptySendBuffer;
        [FormerlySerializedAs("throttleTime")] public int throttleStartTime;
        public int packetCountHistorySize;
        public float throttleDecreaseFactor;
        public float maxAllowedDeviation; // Define spike threashold (example)
        public float agressiveThrottlingDecreaseFactor;
        
        Stopwatch packetCountHistoryTime = new Stopwatch();
        
        [Header("Throttling debug")] 
        public bool logDeviation;
        public float onlyLogAboveDeviationThreshold;
        public bool logSendbufferCount;
        public bool logOnlySendBufferCountWhileThrottling;
        public bool logPacketCountHistory;
        public bool logThrottleDecreaseTime;
        public bool logAgressiveThrottling;
        
        private Stopwatch throttlingWatch = new Stopwatch();
        private Queue<int> packetCountHistory = new Queue<int>();
        private int currentPacketCount;

        
        
        /* tracks deltaTime and timestamps when reliable packets were received, to have a timeout for acks. */
        private float elapsedTime; 

        private ConcurrentQueue<(Packet, IPEndPoint)> sendBuffer = new ConcurrentQueue<(Packet, IPEndPoint)>();
        public Dictionary<int, (int,int)> clientRTT_RTTVAR = new Dictionary<int, (int,int)>();

        private int maxSendBufferSize;
        
        [Header("Debug")] 
        public bool inducePacketLoss;

        public bool closeUdpFlag;
        public int packetLossRate;

        public bool induceDelay;
        public int delay;
        public bool forceUnreliable;


        #region ExternalMethods

        delegate void PacketHandler(Packet packet);
        private Dictionary<PacketType, PacketHandler> packetHandlers;

        public static double CalculateStandardDeviation(List<int> data, int value)
        {
            data.Add(value);
            double mean = data.Average();
            double sumOfSquaredDifferences = data.Select(x => Math.Pow(x - mean, 2)).Sum();
            double standardDeviation = Math.Sqrt(sumOfSquaredDifferences / data.Count);
            
            
            if (value > mean)
            {
                return standardDeviation;
            }
            else
            {
                return 0;
            }
            
            return standardDeviation;
            
            
        }
        
        /// <summary>
        /// Starts the transport layer for UDP.
        /// </summary>
        /// <param name="port">The port number to bind to.</param>
        public override void StartTransport(int port, bool isHost)
        {
     
            packetHandlers = new Dictionary<PacketType, PacketHandler>
            {
                { PacketType.HolePunch, networkClient.HandleHolePunchPacket },
                { PacketType.PublicEndpoint, networkClient.HandlePublicEndpointPacket },
                { PacketType.Ack, HandleAck }
            };

            //The default ports
            closeUdpFlag = false;
            this.port = port;
            udpClient = new UdpClient();
            try
            {
                /* Try if the client can be bound to the default port */
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, this.port));
            }
            catch (Exception e)
            {
                /* Else, Bind unreliable client to a unused port (0 is used to get a free port). */
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            
                this.port = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                if (NetworkManager.instance.ProcedureDebug)
                {
                    Debug.Log("Default port already used. Bind to available port");
                }
            }

            isRunning = true;
            receiveThread = new Thread(ReceivePackets);
            handleSendBufferThread = new Thread(HandleSendBuffer);
            
            receiveThread.Start();
            handleSendBufferThread.Start();
            

            if (NetworkManager.instance.ProcedureDebug)
            {
                Debug.Log("Started Transport! on Port : " + this.port);
            }
            
            throttlingWatch.Start();
            packetCountHistoryTime.Start();
        }
        

        /// <summary>
        /// Stops the transport layer for UDP.
        /// </summary>
        public override void StopTransport()
        {
            closeUdpFlag = true;
        }

        /// <summary>
        /// Invoked each second. Readjusts retransmission rate based on clients network latency to another client.
        /// </summary>
        public void AdjustRetransmission()
        {

            /* RTO (Retransmission timeout) used by TCP */
            /* 1. RTT(n) = (alpha)*RTT(n-1) + (1-alpha) x sampleRTT */
            /* 2. RTTVAR(n) = (beta)*RTTVar(n-1) + (1-beta)|SampleRTT - RTT(n)| */
            /* 3. RTO = RTT(n) + 4*RTTVAR(n) */
            /* alpha = 7/8 = 0.875,   beta = 3/4 = 0.75*/
            /* alpha and beta are weighting factors. Alpha determines how much of the old values are to be remembered. */
            /* the formula acts as a "low pass filter"  */

            float alpha = 0.875f;
            float beta = 0.75f;
            
            int ping = 50;
            int RTTt = (int)((1-alpha) * ping);
            int RTTVARt = (int)((1 - beta) * Math.Abs(ping - RTTt));

            foreach (var client in networkClient.clientList)
            {
                /* Is not going to have to readjust retransmission to itself. Also returns if there is only one client (Rendezvous) in the game. */
                if (client.Key == networkClient.clientID)
                {
                    continue;
                }

                int clientPing = client.Value.ping*2;

                (int, int) old_client_rtt_rttvar;
                if (clientRTT_RTTVAR.TryGetValue(client.Key, out old_client_rtt_rttvar))
                {
                    int oldRTT = old_client_rtt_rttvar.Item1;
                    int oldRTTVAR = old_client_rtt_rttvar.Item2;
                    int newRTT = (int)(alpha * oldRTT + (1-alpha) * clientPing);
                    int newRTTVAR = (int)(beta * oldRTTVAR + (1-beta) * Math.Abs(clientPing - newRTT));
                    (int, int) new_client_rtt_rttvar = (newRTT, newRTTVAR);

                    clientRTT_RTTVAR[client.Key] = new_client_rtt_rttvar;
                }
                else
                {
                    int RTT = (int)((1-alpha) * clientPing);
                    int RTTVAR = (int)((1 - beta) * Math.Abs(clientPing - RTT));
                    clientRTT_RTTVAR.TryAdd(client.Key, (RTT, RTTVAR));
                }
            }
        }
        
        public override void SendPacket(Packet packet, IPEndPoint IPEndpoint, TransportChannel channel)
        {
            PreparePacketForTransmission(packet, IPEndpoint, channel);
        }

        /// <summary>
        /// Sends a packet through UDP.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="iPEndpoint">The endpoint to send the packet to.</param>
        public void PreparePacketForTransmission(Packet packet, IPEndPoint endPoint, TransportChannel channel)
        {
            if (endPoint==null) 
            {
                /* Client has just been removed,ignore transmission of delayed packets. Only log error if client still exists in the clientList. */
                if (networkClient.clientList.ContainsKey(packet.receiverID))
                {
                    Debug.LogError("Can't send packet if endPoint is null. " + "Tried to send to ID: " + packet.receiverID + " with packet type: " + packet.packetType);
                }
                
                /* Was thinking of adding a a counter and a check here if it gets in here contionously somehow, but maybe this might not never happen. */
                return;
            }
            
            /* Sets the channel in the method instead of when creating a packet */
            packet.channel = channel;

            if (NetworkManager.instance.AdvancedDebug) { Debug.Log("Adding packet of type "+packet.packetType+" to sendBuffer!");}

            if (forceUnreliable)
            {
                packet.channel = TransportChannel.Unreliable;
            }
            
            
            /* If reliable transmission, add sequence number to packet. */
            if (packet.channel == TransportChannel.Reliable)
            {
                //Debug.Log("Sent reliable packet with type "+packet.packetType);
                
                //Find the reliableClient of the receiver
                /* Check if receiver exists. */
                if (!networkClient.ClientExists(packet.receiverID))
                {
                    Debug.LogError("Trying to send reliable packet to not yet initialized client");
                    return;
                }

                /* Sets the packet sequence to the next available sequence */
                Client receiverClient = networkClient.clientList[packet.receiverID];
                receiverClient.packetSequenceCounter++;
                packet.sequence = receiverClient.packetSequenceCounter;
            }
            else
            {
                packet.sequence = 0;
            }

            if (packet.packetType != PacketType.Ping && packet.packetType != PacketType.PingReply&& packet.packetType != PacketType.Ack)
            {
                /* Prepare packet for transmission */
                sendBuffer.Enqueue((packet, endPoint));
            }
            else
            {   /* Pings have priority, not to be affected by potential delay such as throttling. */
                TransmitPacket(packet, endPoint);
            }
        }
        
        #endregion

        #region InternalMethods

        /// <summary>
        /// Actually transmits the packet over the network.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="iPEndpoint"></param>
        private void TransmitPacket(Packet packet, IPEndPoint iPEndpoint)
        {
            /* The packet is being sent to someone who is not connected anymore */
            if (!(packet.receiverID == Packet.unspecifiedID || networkClient.clientID == Packet.unspecifiedID) && 
                !networkClient.ClientExists(packet.receiverID) && packet.receiverID != networkClient.serverID)
            {
                Debug.LogWarning("Receiver is not connected.");
                return;
            }
            
            if (packet.channel == TransportChannel.Reliable)
            {
                /* If it is sent over the reliable channel, add the packet to the unAcked list of the client. */
                ConcurrentDictionary<int, (Packet, float, int)> unAckedPackets = networkClient.clientList[packet.receiverID].unAckedPackets;

                if (unAckedPackets.TryGetValue(packet.sequence, out var existingEntry))
                {
                    int attempts = existingEntry.Item3;
                    unAckedPackets[packet.sequence] = (packet, elapsedTime, attempts + 1);
                }
                else
                {
                    unAckedPackets.TryAdd(packet.sequence, (packet, elapsedTime, 0));
                }
                
            }

            /* Serialized and compresses packet */
            byte[] serializedPacket = Packet.Serialize(packet);
            
            iPEndpoint = CheckAndSetRelayEndpoint(iPEndpoint, ref serializedPacket);
            
         
            sendByteCounter += serializedPacket.Length;
            packetsSentCounter++;

            udpClient.Send(serializedPacket, serializedPacket.Length, iPEndpoint);
          
            if (NetworkManager.instance.PacketDebug)
            {
                Debug.Log("Client[" + networkClient.clientID + "]" + " Sending " + packet.packetType +
                          " packet to client" + "[" + packet.receiverID + "]" + " on " + iPEndpoint.ToString() +
                          " On channel " + packet.channel + " with ID :" + packet.sequence);
            }
        }

        private IPEndPoint CheckAndSetRelayEndpoint(IPEndPoint iPEndpoint, ref byte[] serializedPacket)
        {
            if (networkClient.connectionStatus.ContainsKey(iPEndpoint))
            {
                if (networkClient.connectionStatus[iPEndpoint] == ConnectionStatus.Public_Relay)
                {
                    /* If the client does not have a direct connection, serialize the packet including the receiver EP on the end. */
                    serializedPacket = Serializer.SerializeRelayPacket(serializedPacket, iPEndpoint);
                    
                    /* Set the EP to the EP of the server. */
                    iPEndpoint = networkClient.ServerEndpoint;
                }
            }

            return iPEndpoint;
        }


        /// <summary>
        /// Sends a packet as async. Only used for disconnect packets
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="iPEndpoint"></param>
        internal void SendAsyncPacket(Packet packet, IPEndPoint iPEndpoint)
        {
            byte[] serializedPacket = Packet.Serialize(packet);

            iPEndpoint = CheckAndSetRelayEndpoint(iPEndpoint, ref serializedPacket);


            sendByteCounter += serializedPacket.Length;
            /* The receiver is a remote host so I can send to the direct IP. */
            udpClient.SendAsync(serializedPacket, serializedPacket.Length, iPEndpoint);
            if (NetworkManager.instance.PacketDebug)
            {
                Debug.Log("Sent Async Packet");
            }
        }

        /// <summary>
        /// Checks the reliable send buffer for packets to transmit.
        /// </summary>
        protected override void HandleSendBuffer()
        {
            bool throttling = false;
            int throttleDecreaseTime = 0;
            float throttleDecreaseFactorLocal = 0;
            while (isRunning)
            {
                while (sendBuffer.Count > 0)
                {
                    /*if (logSendbufferCount && !logOnlySendBufferCountWhileThrottling)
                    {
                        Debug.Log("SendBuffer count: " + sendBuffer.Count);
                        Thread.Sleep(timeToEmptySendBuffer);
                    }
                    */
                    /*
                    if (throttling && adaptiveThrottling)
                    {
                        if (checkIfThrottle())
                        {
                        */
                            /* If already throttling, apply more agressive time degration. */
                       /*
                                if (logAgressiveThrottling)
                                {
                                    throttleDecreaseFactorLocal *= agressiveThrottlingDecreaseFactor*sendBuffer.Count;
                                    Debug.Log("Multiplying with agressive throttling decrease, decreaseFacor is now: " +
                                              throttleDecreaseFactorLocal);
                                }

                            
                        }
                    }

                    if (!throttling && checkIfThrottle() && adaptiveThrottling)
                    {
                        Debug.Log("enabled throttling");
                        throttleDecreaseTime = throttleStartTime;
                        throttleDecreaseFactorLocal = throttleDecreaseFactor*sendBuffer.Count;
                        throttling = true;
                    }
                    */
                     
                    /*if (throttling)
                    {
                        throttleDecreaseTime = (int)(throttleDecreaseTime / throttleDecreaseFactorLocal);
                        if (throttleDecreaseTime == 0)
                        {
                            throttling = false;
                        }
                        else
                        {
                            Thread.Sleep(throttleDecreaseTime);
                        }

                        if (logThrottleDecreaseTime)
                        {
                            Debug.Log("throttle decrease time: " + throttleDecreaseTime);
                        }

                        if (logOnlySendBufferCountWhileThrottling)
                        {
                            Debug.Log("SendBuffer count: " + sendBuffer.Count);
                        }
                    }
                    */

                    (Packet, IPEndPoint) packet;
                    
                    sendBuffer.TryDequeue(out packet);

                    if (induceDelay)
                    {
                        Task.Run(() => TransmitDelayedPacket(packet.Item1, packet.Item2));
                    }
                    else
                    {
                        TransmitPacket(packet.Item1, packet.Item2);
                    }
      
                }

              //  throttling = false;
                //throttleDecreaseFactorLocal = 0;
            }
            
        }
        public bool checkIfThrottle()
        {
            currentPacketCount = sendBuffer.Count;
            
            /*if (packetCountHistoryTime.ElapsedMilliseconds >= UpdatePacketCountHistoryMS)
            {
                while (packetCountHistory.Count > 0)
                {
                    packetCountHistory.Dequeue();
                }
                
            }
            */
            String k = "";
         
                double deviation = CalculateStandardDeviation(packetCountHistory.ToList(), currentPacketCount);

                
                if (logPacketCountHistory && (deviation >= onlyLogAboveDeviationThreshold))
                {
                    string s = "[";
                    foreach (var v in packetCountHistory)
                    {
                        s += v + ",";
                    }
                 //   s += "]";

                 k = s + " ";
                 if (logDeviation && (deviation >= onlyLogAboveDeviationThreshold))
                 {
                     k += " currentPacketCount: " + currentPacketCount + "deviation: " + deviation + "\n";
                     Debug.Log(k);
                 }
                 else
                 {
                     Debug.Log(s);
                 }
                }

               
                    // [3 5 2 5] (check new if throttle)
                    // if throttle, throttle and discard it.
                    // [5 2 5 3]
                    
                if (deviation >= maxAllowedDeviation)
                {
                    Debug.Log("Spike detected.  ");
                    //packetCountHistory.Dequeue();
                    while (packetCountHistory.Count > 0)
                    {
                        packetCountHistory.Dequeue();
                    }
                    packetCountHistory.Enqueue(currentPacketCount);
                    /* currentPacketCount is ignored (as it has been throttled when sendning) */
                    return true; 
                }

                if (packetCountHistory.Count != packetCountHistorySize)
                {
                    packetCountHistory.Enqueue(currentPacketCount);
                    currentPacketCount = 0;
                    return false;
                }
                else
                {
                    currentPacketCount = 0;
                    packetCountHistory.Dequeue();
                    return false;
                }


        }
        
        public async Task TransmitDelayedPacket(Packet packet, IPEndPoint iPEndpoint)
        {
            await Task.Delay(delay);
            TransmitPacket(packet, iPEndpoint);


        }
        

        /// <summary>
        /// Checks the waitingForAck list for packets that need to be retransmitted.
        /// </summary>
        protected override void HandleSendPacketAckUpdate()
        {
            if (networkClient == null)
            {
                return;
            }
            
            if (networkClient.clientList==null)
            {
                return;
            }
            foreach (var client in networkClient.clientList)
            {
                /* Should not retransmit anything to itself. */
                
                //Skit i denna klienten om dess ping är för hög
                
                if (client.Key == networkClient.clientID)
                {
                    continue;
                }

                ConcurrentDictionary<int, (Packet, float, int)> unackedPackets = client.Value.unAckedPackets;
                /* If client is not expecting any acks, continue */
                if (unackedPackets.Count == 0) continue;

                //Make a copy since we cant iterate over the actual keys. If we do, they will change. Not good
               Dictionary<int, (Packet, float, int)> tempUnackedPackets = new Dictionary<int, (Packet, float, int)>(unackedPackets); 

                foreach (var unackedPacket in tempUnackedPackets)
                {
                    /* If x retransmission have already taken place, establishing a reliable connection failed, remove unAcked packet.*/
                    if (unackedPacket.Value.Item3 > maximumRetransmissions)
                    {
                        Debug.LogWarning("Retransmission limit reached for packet " + " to: " + unackedPacket.Key);

                        (Packet, float, int) removed;
                        unackedPackets.TryRemove(unackedPacket.Key,out removed);

                    }
                    
                    int receiverID = unackedPacket.Value.Item1.receiverID;
                    /* Update packet timeout */
                    if (clientRTT_RTTVAR.ContainsKey(receiverID))
                    {
                        int timeout = packetTimeout;
                        
                        if (unackedPackets.ContainsKey(unackedPacket.Key))
                        {
                            timeout = clientRTT_RTTVAR[receiverID].Item1 + 4 * clientRTT_RTTVAR[receiverID].Item2;
                        }
                        else
                        {
                            timeout = packetTimeout;
                        }
    
                        if (elapsedTime - unackedPacket.Value.Item2 > ((float)timeout/1000.0f))
                        {
                            
                           // UnityEngine.Debug.Log("Should retransmit");
                            (Packet, float, int) output;
                            //Double check if the packet still exists. (The packet could have been received while iterating due to threading)
                            if (unackedPackets.TryGetValue(unackedPacket.Key, out output))
                            {
                                //Debug.Log("i didnt get here.");
                                RetransmitPacket(output.Item1, networkClient.GetEndpoint(output.Item1.receiverID));
                            }
                        }
                    }
                    
        
                }
            }

            elapsedTime += Time.deltaTime;
        }

        /// <summary>
        /// Handles the Ack packet and removes it from the unAckedPackets list.
        /// </summary>
        /// <param name="packet">The Ack packet to validate.</param>
        private void HandleAck(Packet packet)
        {
            /* The sequence is saved in the data in an ack */
            int sequence = Serializer.DeserializeAckPacket(packet.data);

            /* If someone left after sending a reliable packet */
            if (!networkClient.ClientExists(packet.senderID))
            {
                Debug.LogWarning("Received ack for invalid client!");
                return;
            }

            ConcurrentDictionary<int, (Packet, float, int)> sendersUnackedPackets =
                networkClient.clientList[packet.senderID].unAckedPackets;
            
            if (sendersUnackedPackets.ContainsKey(sequence))
            {
                if (NetworkManager.instance.PacketDebug)
                {
                    Debug.Log("Received Ack from " + packet.senderID + " for packet " + packet.sequence);
                }

                (Packet, float, int) removed;
                sendersUnackedPackets.TryRemove(sequence,out removed);
            }
            else
            {
                if (NetworkManager.instance.PacketDebug)
                {
                    Debug.LogWarning("Received Ack for already acked packet");
                }
            }
        }

        /// <summary>
        /// Retransmits a reliable packet to the given endpoint.
        /// </summary>
        /// <param name="packet">The packet to retransmit.</param>
        /// <param name="iPEndpoint">The endpoint to retransmit the packet to.</param>
        private void RetransmitPacket(Packet packet, IPEndPoint iPEndpoint)
        {
            lostPacketCounter++;
            /* Places the packet first in line. Retransmissions have priority in order not to block traffic.  */
            sendBuffer.Enqueue((packet, iPEndpoint));
            
            
            //Debug.Log("Retransmitting packet to "+ packet.receiverID +  " packetType: "+ packet.packetType);

        }

        /// <summary>
        /// Handles reliable packets. Will send ack and add packets to receive buffer.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="ipEndPoint"></param>
        private void HandleReliablePacket(Packet packet)
        {
            if (!networkClient.ClientExists(packet.senderID)) 
            {
                return;
            }

            //Send Ack
            Packet ackPacket = new Packet(PacketType.Ack, packet.senderID, networkClient.clientID, sizeof(int), Serializer.SerializeAckPacket(packet.sequence));
            PreparePacketForTransmission(ackPacket, networkClient.GetEndpoint(packet.senderID), TransportChannel.Unreliable);

            if (NetworkManager.instance.AdvancedDebug) Debug.Log("Sending ack for packet " + packet.sequence);


            Client SenderClient = networkClient.clientList[packet.senderID];
            lock (SenderClient.receiveBufferLock)
            {
                /* IMPORTANT : Need to do manual check since the contains() doesnt work all the time due to threading */
                if (SenderClient.IsInReliableReceiveBuffer(packet) || packet.sequence <= SenderClient.lastHandledPacket)
                {
                    if (NetworkManager.instance.AdvancedDebug) Debug.Log("Discard already handled packet!");
                    return;
                }

                SenderClient.sortedReliableReceiveBuffer.Add(packet);
            }
        }




        /// <summary>
        /// Iterations through the receiveBuffers for each client and handles packet if it is the right order.
        /// </summary>
        protected override void HandleOrderedReliableReceiveBuffer()
        {
            if (networkClient == null)
            {
                return;
            }
            
            if (networkClient.clientList==null)
            {
                return;
            }
   
            foreach (var value in networkClient.clientList)
            {
                
                int clientID = value.Key;
                Client client = value.Value;
                
                /* Client is not sending anything to itself. */
                if (clientID == networkClient.clientID)
                {
                    continue;
                }
                
                

                lock (client.receiveBufferLock)
                {
                    
                    /* Check if reliable packet has been received from client x, if not continue with next.  */
                    if (client.receiveBufferIsEmpty())
                    {
                        continue;
                    }
                    
                    /* Sort it to get the packets in the right order. i.e smallest packet sequence first */
                
                    /* The first packet. i.e packet with the smallest packet sequence */
                    Packet packet = client.sortedReliableReceiveBuffer.Min;

                    if (client.sortedReliableReceiveBuffer.Count > 1)
                    {
                        //Debug.Log(" ==== Reliabel received buffer === ");
                        foreach (var VAR in client.sortedReliableReceiveBuffer)
                        { 
                       
                            //Debug.Log("Packet type: " + VAR.packetType );
                            //Debug.Log("Packet sequence: " + VAR.sequence );
                        }
                    }

                    /* Handle every packet that comes in the right order, i.e the sequence is one higher than the last handled packet.*/
                    while (packet.sequence == client.lastHandledPacket + 1)
                    {
                        networkClient.HandlePacket(packet, networkClient.GetEndpoint(packet.senderID));
                        client.lastHandledPacket = packet.sequence;

                        client.sortedReliableReceiveBuffer.Remove(packet);
                    
                        /* If the receive  buffer is not yet empty, we keep on going until its empty or wrong order. */
                        if (client.receiveBufferIsEmpty())
                        {
                            break;
                        }
                    
                        packet = client.sortedReliableReceiveBuffer.Min;
                    }

                }
                
            }
        }

        /// <summary>
        /// Receives packets through the UDPClient. 
        /// </summary>
        private void ReceivePackets()
        {
            Random random = new Random();

            while (isRunning)
            {
                try
                {
                    if (NetworkManager.instance.AdvancedDebug) Debug.Log("Received Packet ");
                    receivedEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    /* Sets a receive timeout for the udpClient, this so it can be closed properly after timeout has been reached */
                    udpClient.Client.ReceiveTimeout = 500;
                    byte[] data = null;
                    try
                    {
                        data = udpClient.Receive(ref receivedEndPoint);
                    }
                    catch (SocketException se)
                    {
                        /* WSAETIMEDOUT, when it gets here it can be closed properly without receiving WSACancelBlockingCall */
                        if (se.ErrorCode == 10060)
                        {
                            if (closeUdpFlag)
                            {
                                udpClient.Close();
                                isRunning = false;
                                if (NetworkManager.instance.SocketDebug)
                                {
                                    Debug.LogError("TRANSPORT ACTUALLY CLOSED");
                                }

                                break;
                            }

                            /* Timeout occurred, no data received, continue the loop */
                            continue;

                        }

                        throw new Exception($"SocketException occurred: {se.Message}");

                    }


                    if (inducePacketLoss)
                    {
                        if (random.NextDouble() * 100 > packetLossRate)
                        {
                            continue;
                        }
                    }

                    Packet packet = Packet.DeSerialize(data);
                    if (NetworkManager.instance.AdvancedDebug)
                    {
                        Debug.Log("Packet was sent from " + receivedEndPoint.ToString() + " with ID " +
                                  packet.senderID);
                    }

                    if (NetworkManager.instance.AdvancedDebug)
                    {
                        Debug.Log("Packet was type " + packet.packetType);
                    }

                    /* Updating statistics */
                    receiveByteCounter += data.Length;
                    packetsReceivedCounter++;

                    bool senderSpecified = packet.senderID != Packet.unspecifiedID;
                    bool receiverSpecified = packet.receiverID != Packet.unspecifiedID;
                    bool clientSpecified =
                        networkClient.clientID != Packet.unspecifiedID; // todo when does this even happen??
                    bool senderConnected = networkClient.ClientExists(packet.senderID);
                    bool senderNotServer = packet.senderID != networkClient.serverID;

                    bool inValidPacket = !(senderSpecified || receiverSpecified || clientSpecified) &&
                                         !senderConnected && senderNotServer;

                    if (inValidPacket)
                    {
                        if (NetworkManager.instance.PacketDebug)
                        {
                            Debug.LogWarning("Sender is not connected.");
                        }

                        continue;
                    }


                    if (packet.channel == TransportChannel.Reliable)
                    {
                        HandleReliablePacket(packet);
                    }
                    else
                    {
                        // Check if the packet type has an associated handler
                        if (packetHandlers.TryGetValue(packet.packetType, out PacketHandler handler))
                        {
                            handler(packet);
                        }
                        else
                        {
                            networkClient.HandlePacket(packet, receivedEndPoint);
                        }
                    }


                    /* Debug */
                    if (NetworkManager.instance.AdvancedDebug)
                        Debug.Log("Client[" + networkClient.clientID + "]" + " Received " + packet.packetType +
                                  " packet from client" + "[" + packet.senderID + "]" + " of " + data.Length +
                                  " bytes" + "! On channel : " + (TransportChannel)(packet.channel));
                }
                catch (SocketException se)
                {
                    Debug.Log("SocketException: " + se.Message + "\n" + se.StackTrace);
                    Debug.Log("Client disconnected or sent a failed packet.");
                }

                catch (NullReferenceException)
                {
                    
                    
                }
                
                catch (Exception e)
                {
                    Debug.LogError("Exception: " + e.Message + "\n" + e.StackTrace);
                    Debug.Log("Received failed packet!");
                }
                
                
            }
            receiveThread.Interrupt(); // Todo this is appropiate place?
        }

        #endregion
    }
}