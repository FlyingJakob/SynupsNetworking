using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using SynupsNetworking.components;

namespace SynupsNetworking.core
{

    public class PacketSorter : IComparer<Packet>
    {
        public int Compare(Packet x, Packet y)
        {
            
            if (x.sequence < y.sequence)
            {
                return -1;
            }

            if (x.sequence > y.sequence)
            {
                return 1;
            }

            return 0;
           
        }
    }

    public class Client
    {
        public ConcurrentDictionary<int, (Packet, float,int)> unAckedPackets;
        public SortedSet<Packet> sortedReliableReceiveBuffer;
        public int packetSequenceCounter;
        public int lastHandledPacket;
        public IPEndPoint publicEP;
        public IPEndPoint privateEP;
        public object receiveBufferLock = new object();
        
        public NetworkIdentity localPlayer;
        public int ping;
        public int failedPingReplies;
        public bool pingReplyFlag;
        public bool pingReplyReceived;
        public Client()
        {
            unAckedPackets = new ConcurrentDictionary<int, (Packet, float,int)>();
            PacketSorter packetSorter = new PacketSorter();
            sortedReliableReceiveBuffer = new SortedSet<Packet>(packetSorter);
            packetSequenceCounter = 0;
            lastHandledPacket = 0;
            ping = 999;
            failedPingReplies = 0;
            pingReplyReceived = false;
        }
        
        public Client(IPEndPoint publicEP,IPEndPoint privateEP)
        {
            this.publicEP = publicEP;
            this.privateEP = privateEP;
            PacketSorter packetSorter = new PacketSorter();
            sortedReliableReceiveBuffer = new SortedSet<Packet>(packetSorter);
            unAckedPackets = new ConcurrentDictionary<int, (Packet, float, int)>();
            pingReplyReceived = false;
            ping = 999;
            failedPingReplies = 0;
        }

        

        public bool receiveBufferIsEmpty()
        {
            return sortedReliableReceiveBuffer.Count == 0;
        }
        
        public bool IsInReliableReceiveBuffer(Packet packet)
        {

            return sortedReliableReceiveBuffer.Contains(packet);
            foreach (var pk in sortedReliableReceiveBuffer)
            {
                if (packet.sequence == pk.sequence)
                {
                    return true;
                }
            }
            return false;
        }


    }
}