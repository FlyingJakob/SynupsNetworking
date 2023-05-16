using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using SynupsNetworking.core.Enums;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace SynupsNetworking.core.Misc
{
    /* Keeps settings of Ping functionality and keeps track of ping replies. */

    public class PingManager : MonoBehaviour
    {
        public static PingManager instance;
        private Stopwatch replyStopwatch;
        private Stopwatch pingStopwatch;
        /* Per default amount of pings to be sent is 4. */
        private const int DEFAULT_PING_COUNT = 4;
        private const int DEFAULT_REPLY_WAIT_TIME_MS = 200; 
        private const int FIXED_TRANSMISSION_DELAY_MS = 2;
        public bool replyFlag;

        public bool timeoutFlag;
        public int receivedReplies;
        public int failedReplies;
        private SemaphoreSlim semaphore;
        public int pingCountField{ get;  set; }
        public int bytesField{ get;  set; }
        public int fixedTransmissionDelay{ get;  set; }
        public int replyTimeoutMSField{ get;  set; }
        // public float setBytes;
        public bool useSocketCheckBox;
        public int receivingClientIDField{ get;  set; }
        public bool defaultValueCheckBox;
        public bool advancedValueCheckBox;
        public bool showRepliesCheckBox;
        public bool Pinging;
        public string AddressField{ get;  set; }
        public int PortField{ get;  set; }
        public int packetDataLength;
        public int totalHandledPackets;
        private float min, avg, max, mdev;
        private int tempReceiverID;
        private int replyTimeout{ get; set; }
        private int expectedReplies { get; set; }

        public bool isRunning;
        private IPEndPoint receivingEndPoint;
        private List<float> timeReply;
        private void Awake()
        {
            if (instance == null)
                instance = this;
            
            pingCountField = DEFAULT_PING_COUNT;
            replyTimeoutMSField = DEFAULT_REPLY_WAIT_TIME_MS;
            fixedTransmissionDelay = FIXED_TRANSMISSION_DELAY_MS;
            
            receivingClientIDField = 0;
            defaultValueCheckBox = true;
            useSocketCheckBox = false;
            isRunning = false;
            replyStopwatch = new Stopwatch();
            pingStopwatch = new Stopwatch();
            replyFlag = false;
            timeoutFlag = false;
            showRepliesCheckBox = true;
            totalHandledPackets = 0;
            bytesField = 0;

            timeReply = new List<float>();
            replyTimeout = 0;
            expectedReplies = 0;
     
            semaphore = new SemaphoreSlim(1);
        }

        public void init() // Invoked from NetworkManager, Ensures networkClient is not null.
        {
            //waitForPingReplyThread.Start();
            
        }

        public void DefaultValueCheck() /* Invoked from editor. */
        {
            if (defaultValueCheckBox)
            {
                pingCountField = DEFAULT_PING_COUNT;
                replyTimeoutMSField = DEFAULT_REPLY_WAIT_TIME_MS;
                bytesField = 0;
            }
 
        }

        public void AdvancedCheck() /* Invoked from editor. */
        {
            if (!advancedValueCheckBox)
            {
                fixedTransmissionDelay = FIXED_TRANSMISSION_DELAY_MS;
            }
        }

        public async void ButtonSendPing()
        {
            if (Pinging) /* Avoids sending pings multiple times */
            {
                return;
            }
        
            Pinging = true;

            if (!useSocketCheckBox)
            {
                receivingEndPoint = NetworkClient.instance.ResolveClientID(receivingClientIDField);
            }
            else
            {
                receivingEndPoint = new IPEndPoint(IPAddress.Parse(AddressField), PortField);
            }
            
            await ButtonSendPingAsync(receivingEndPoint, pingCountField);
        }

        private float calcMeanD()
        {
            float sum = 0, mean = 0, variance = 0, stdDeviation = 0;

            if (timeReply.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < timeReply.Count; i++)
            {
                sum += timeReply.IndexOf(i);
            }

            mean = sum / timeReply.Count;
            

            for (int i = 0; i < timeReply.Count; i++)
            {
                variance += (float)Math.Pow(timeReply.IndexOf(i) - mean, 2);
            }

          
            variance = variance / timeReply.Count;

            stdDeviation = (float)Math.Sqrt(variance);

            return (float) Math.Round(avg, 3);

        }

        #region BtnSendPings

         /// <summary>
        /// Sends an unreliable packet with a specified delay.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="iPEndpoint">The remote endpoint to send the packet to.</param>
        /// <param name="delay">The delay in milliseconds before sending the packet.</param>
        private async Task ButtonSendPingAsync(IPEndPoint endPoint, int count)
        {
            min = 0;
            avg = 0;
            max = 0;
            mdev = 0;
            
            receivedReplies = 0;
            failedReplies = 0;
            totalHandledPackets = 0;
            replyTimeout = replyTimeoutMSField;
            expectedReplies = pingCountField;

  
        
            /* seqNum MUST start at one not zero as pings with seq 0 is used for checking delays between clients and show this in leaderboard. */
            for (int seqNum = 1; seqNum < count+1; seqNum++)
            {
                // Delay sending the packet without blocking the main thread. 
                await Task.Delay(fixedTransmissionDelay);
                    
                /* This await statement waits asynchronously until semaphore is release (=> last ping reply is handled)
                 This is done to prevent a new ping from affecting the replyFlag causing issues with the previous ping that is being handled. */
                await semaphore.WaitAsync(); 
                Task.Run(async () => {replyTimeThread(true); });
                

                byte[] seqNumber = BitConverter.GetBytes(seqNum);
                Packet packet = new Packet(PacketType.Ping, receivingClientIDField, NetworkClient.instance.clientID, seqNumber.Length,  seqNumber);

                if (seqNum == 1)
                {
                    Debug.Log("Pinging " + "[" + tempReceiverID + "] " + "[" + receivingEndPoint + "]" + " with " +
                              Packet.Serialize(packet).Length + " bytes of data:");
                }

                if (!defaultValueCheckBox && bytesField > 0)
                {
                    seqNumber = packet.appendBytes(bytesField);
                }
                NetworkClient.instance.SendPacket(packet, NetworkClient.instance.GetEndpoint(receivingClientIDField),TransportChannel.Unreliable);
            }
        }
         #endregion
         public async Task<double> replyTimeThread(bool showStats)
        {
            float elapsedTime = 0;
            int totalDelay = 0;
            
         
            elapsedTime = 0; /* Reset elapsedTime for each handled or dropped replies. */
            replyStopwatch.Start(); /* Timestamp */
            pingStopwatch.Start();
            replyFlag = false;
            totalDelay = replyTimeout;
            
                while (elapsedTime < totalDelay)
                {

                   // Debug.Log(elapsedTime + " time");
                    if (replyFlag)
                    {
                        receivedReplies++;
                        totalHandledPackets++;
                        timeReply.Add(elapsedTime);

                        if (showRepliesCheckBox && NetworkManager.instance.PingDebug)
                        {
                            Debug.Log("Reply from " + "[" + tempReceiverID + "]" + " [" + receivingEndPoint +
                                      "] " + " bytes=" + packetDataLength + " time=" + (elapsedTime) + "ms");
                        }

                        break;
                    }

                    elapsedTime = (float)replyStopwatch.Elapsed.TotalMilliseconds;
                }
        

            if (elapsedTime >= totalDelay)
            {
                timeoutFlag = true;
                failedReplies++;
                totalHandledPackets++;
                
            }
            else
            {
                if (receivedReplies == 1)
                {
                    min = elapsedTime;
                }
                else
                {
                    if (min > elapsedTime)
                    {
                        min = elapsedTime;
                    }
                }

                if (max < elapsedTime)
                {
                    max = elapsedTime;
                }

                avg += elapsedTime;
            }

            if (showRepliesCheckBox && timeoutFlag)
                Debug.Log("Request timed out.");

                /* Then we are finished with all replies. */
                if (totalHandledPackets == expectedReplies)
                {
                    // Todo this might have been redundant
                    //networkClient.expectedReplyFromClientID =  -1; /* Client should no longer expect replies from the receiver.  */

                    try
                    {
                        avg = (float) avg  / (float) receivedReplies;
                    } 
                    catch (DivideByZeroException)
                    {
                        avg = 0;
                    }

                    if (showStats)
                    {
                        Debug.Log("Ping statistics for " + "[" + tempReceiverID + "]" + " [" + receivingEndPoint +
                                  "]: " + Environment.NewLine +
                                  "Packets: Sent = " + pingCountField + ", Received = " + receivedReplies +
                                  ", Lost = " + failedReplies +
                                  "(" + (float)failedReplies / (float)pingCountField * 100 + "%" + " loss), " +
                                  "time = " + elapsedTime + "ms" + ", " +
                                  "min/avg/max/meand = " + Math.Round(min, 3) + "/" + Math.Round(avg, 3) + "/" +
                                  Math.Round(max, 3) + "/" + calcMeanD() + " ms");
                    }

                    timeReply.Clear();
                    pingStopwatch.Reset();
                    Pinging = false;
                    
                    receivedReplies = 0;
                    failedReplies = 0;
                    totalHandledPackets = 0;
                   
                    
                }
              
                /* Finished waiting for this reply */
                replyStopwatch.Reset();
                pingStopwatch.Stop();
                isRunning = false;
                semaphore.Release();
                Pinging = false;
                
                if (timeoutFlag)
                {
                    timeoutFlag = false;
                    return 999;
                }
                else
                {
                    return elapsedTime;
                }

        }

    }
}