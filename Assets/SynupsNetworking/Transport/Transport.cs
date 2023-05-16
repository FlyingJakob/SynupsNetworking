using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using SynupsNetworking.core;
using SynupsNetworking.core.Enums;
using UnityEngine;
using UnityEngine.Serialization;

namespace SynupsNetworking.transport
{
    public abstract class Transport : MonoBehaviour
    {
        public NetworkClient networkClient;
        internal int port;
        public bool isRunning { get; internal set; }

        #region Stats

        
        public float sendBytesPerSecond{ get; private set; }
        public float receiveBytesPerSecond{ get; private set; }
        
        public int packetsSentPerSecond{ get; private set; }
        
        public int packetsReceivedPerSecond{ get; private set; }
            
        internal int sendByteCounter;
        internal int receiveByteCounter;
        
        internal int packetsSentCounter;
        internal int packetsReceivedCounter;

        internal int lostPacketCounter;
        internal float lostPacketPercentage;


        public float statstimeCounter;


        private void Start()
        {
            if (Application.isBatchMode)
            {
                StartCoroutine(printBatch());
            }
        }

        private IEnumerator printBatch()
        {
            while (!isRunning)
            {
                yield return null;
            }

            while (true)
            {
                string text = "";


                Dictionary<int, Client> tempClientList = new Dictionary<int, Client>(NetworkClient.instance.clientList);
                Console.Clear();
                Console.Write("\r");
                Console.WriteLine("Send : "+sendBytesPerSecond+"   Receive : "+receiveBytesPerSecond+" PacketLoss : "+lostPacketPercentage*100);

                foreach (var kvp in tempClientList)
                {
                    text=("Client "+kvp.Key+" : "+"    Status: "+NetworkClient.instance.connectionStatus[kvp.Value.publicEP]+"    ping : "+kvp.Value.ping);

                    if (NetworkClient.instance.connectionStatus[kvp.Value.publicEP]==ConnectionStatus.Mine)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;

                    }
                    else
                    {
                        if (kvp.Value.ping>500)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                        }
                    }
                    
                    
                    
                    
                    
                    
                    Console.WriteLine(text);
                    Console.ResetColor();

                }
                
                
                
                
                yield return new WaitForSeconds(2);

            }
            
            
        }

        private void CalculateStatsUpdate()
        {
            if (isRunning)
            {
                statstimeCounter += Time.deltaTime;
                if (statstimeCounter>0.5f)
                {
                    sendBytesPerSecond = sendByteCounter / statstimeCounter;
                    receiveBytesPerSecond = receiveByteCounter / statstimeCounter;
                    packetsReceivedPerSecond = (int)(packetsReceivedCounter / statstimeCounter);
                    packetsSentPerSecond = (int)(packetsSentCounter / statstimeCounter);
                    lostPacketPercentage =packetsSentCounter==0?0: (float)(((float)lostPacketCounter) / (float)packetsSentCounter);
                    Debug.Log(lostPacketPercentage);
                    lostPacketCounter = 0;
                    sendByteCounter = 0;
                    receiveByteCounter = 0;
                    packetsReceivedCounter = 0;
                    packetsSentCounter = 0;
                    statstimeCounter = 0;

                    if (GetComponent<StatisticsManager>())
                    {
                        GetComponent<StatisticsManager>().AddStatistics(sendBytesPerSecond, receiveBytesPerSecond,
                            packetsSentPerSecond, packetsReceivedPerSecond,lostPacketPercentage);
                    }
                    
                }
            }
        }
            
        
        private void Update()
        {
            if (isRunning)
            {
                CalculateStatsUpdate();
                HandleOrderedReliableReceiveBuffer();
                HandleSendPacketAckUpdate();
            }
            
        }
        
        #endregion

        protected abstract void HandleSendBuffer();
        protected abstract void HandleOrderedReliableReceiveBuffer();

        protected abstract void HandleSendPacketAckUpdate();
        
        public abstract void StartTransport(int port,bool isHost);

        public abstract void StopTransport();
   
        public abstract void SendPacket(Packet packet, IPEndPoint IPEndpoint,TransportChannel channel);
       

    }
}