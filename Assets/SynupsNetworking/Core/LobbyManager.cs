using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SynupsNetworking.core.Enums;
using SynupsNetworking.core.Misc;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

namespace SynupsNetworking.core
{
    public class LobbyManager
    {
        public static LobbyManager instance { get; set; }

        private IPEndPoint serverEP;
        private UdpClient client;
        
        private Stopwatch pingwatch;
        
        public LobbyInformation LobbyInformation;
        private Stopwatch latencyMeasure;

        public Thread receiveThread;
        public LobbyManager(IPEndPoint serverEp)
        {
            this.serverEP = serverEp;
            instance = this;
            client = new UdpClient();
            client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            latencyMeasure = new Stopwatch();
            receiveThread = new Thread(ReceivePackets);
            receiveThread.Start();
        }
        
        

        public LobbyInformation[] lobbyInformationResult;
        private UnityAction<LobbyInformation[]> onReceiveLobbyInformation;
        public void RequestLobbyInformation(UnityAction<LobbyInformation[]> onReceiveLobbyInformation)
        {
            this.onReceiveLobbyInformation = onReceiveLobbyInformation;
            Packet packet = new Packet(PacketType.LobbyInformationRequest, -1, -1, 0, new byte[0]);
            byte[] serializedPacket = Packet.Serialize(packet);
            
            client.Send(serializedPacket,serializedPacket.Length,serverEP);
            latencyMeasure.Start();
        }

        public void SetLobbyInformation(LobbyInformation lobbyInformation)
        {
            
            byte[] lobbyInfo = Serializer.SerializeLobbyInformation(lobbyInformation);
            Packet lobbyPacket = new Packet(PacketType.LobbyInformationSet, -1, -1, lobbyInfo.Length, lobbyInfo);

            byte[] serializedPacket = Packet.Serialize(lobbyPacket);
            
            client.Send(serializedPacket, serializedPacket.Length, serverEP);

        }

        public void Stop()
        {
            receiveThread.Interrupt();
        }
        
        
        public void ReceivePackets()
        {
            while (true)
            {
                try
                {
                    IPEndPoint receivedEP = null;
                    byte[] data = client.Receive(ref receivedEP);
          
                    Packet packet = Packet.DeSerialize(data);

                    switch (packet.packetType)
                    {
                        case PacketType.LobbyInformationSet:
                            latencyMeasure.Stop();
                            int experiencedLatency = (int)latencyMeasure.ElapsedMilliseconds;
                            latencyMeasure.Reset();
                            
                            NetworkManager.instance.EnqueueAction(() =>
                            {
                                ReceiveLobbyInformation(packet,experiencedLatency);
                            });
                            break;
                    }




                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private void ReceiveLobbyInformation(Packet packet, int latency)
        {
           
            lobbyInformationResult = Serializer.DeserializeLobbyInformationList(packet.data);
          
            foreach (var v in lobbyInformationResult)
            {
                v.delay += latency;
            }
            onReceiveLobbyInformation.Invoke(lobbyInformationResult);
        }
    }
}