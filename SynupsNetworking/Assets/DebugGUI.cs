using System;
using System.Collections.Generic;
using System.Net;
using SynupsNetworking.core;
using SynupsNetworking.core.Enums;
using SynupsNetworking.transport;
using UnityEngine;

namespace SynupsNetworking.components
{
    public class DebugGUI : MonoBehaviour
    {
        
        
        private Vector2 scrollPos;


        private void OnGUI()
        {
        
            if (NetworkManager.instance==null)
            {
                return;
            }
            else
            {
                if (NetworkManager.instance.isConnected)
                {
                    if (GUILayout.Button("Leave",GUILayout.Width(100)))
                    {
                        NetworkManager.instance.Disconnect();
                    }
                }
                else
                {
                    if (GUILayout.Button("Join",GUILayout.Width(100)))
                    {
                        NetworkManager.instance.JoinSession();
                    }
                    if (GUILayout.Button("Host",GUILayout.Width(100)))
                    {
                        NetworkManager.instance.CreateSession();
                    }
                }
                
                
                if (NetworkManager.instance.networkClient==null)
                {
                    
                    return;
                }
                else
                {
                    if (NetworkManager.instance.networkClient.clientList==null)
                    {
                        return;
                    }
                }
            }

            NetworkClient networkClient = NetworkManager.instance.networkClient;
            
            
            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(1500), GUILayout.Height(700));
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("ClientID", GUILayout.Width(150));
            GUILayout.Label("Public Endpoint", GUILayout.Width(150));
            GUILayout.Label("Private Endpoint", GUILayout.Width(150));
            GUILayout.Label("Connection Status", GUILayout.Width(150));
            GUILayout.Label("Ping", GUILayout.Width(150));
            GUILayout.Label("Receive Buffer Length", GUILayout.Width(150));
            GUILayout.Label("Packet timeout", GUILayout.Width(150));
            GUILayout.EndHorizontal();
            foreach (KeyValuePair<int, Client> entry in NetworkManager.instance.networkClient.clientList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.Key.ToString(), GUILayout.Width(150));
                GUILayout.Label(entry.Value.publicEP.ToString(), GUILayout.Width(150));
                GUILayout.Label(entry.Value.privateEP.ToString().ToString(), GUILayout.Width(150));
                GUILayout.Label(networkClient.connectionStatus.ContainsKey(entry.Value.publicEP)?networkClient.connectionStatus[entry.Value.publicEP].ToString():"Null", GUILayout.Width(150));
                GUILayout.Label(networkClient.connectionStatus[entry.Value.publicEP] != ConnectionStatus.Mine ? entry.Value.ping.ToString() : "0");
                GUILayout.Label(entry.Value.sortedReliableReceiveBuffer != null ? entry.Value.sortedReliableReceiveBuffer.Count+"" : "null");

                if ((NetworkManager.instance.transport as UDPTransport).clientRTT_RTTVAR!=null)
                {
                    if ((NetworkManager.instance.transport as UDPTransport).clientRTT_RTTVAR.ContainsKey(entry.Key))
                    {
                        GUILayout.Label(
                            ((NetworkManager.instance.transport as UDPTransport).clientRTT_RTTVAR[entry.Key].Item1 + 4 *
                                (NetworkManager.instance.transport as UDPTransport).clientRTT_RTTVAR[entry.Key].Item2)+"");
                    }
                }
                
               
                GUILayout.EndHorizontal();
            }

            
            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            
            float bytesSent = NetworkManager.instance.transport.sendBytesPerSecond;
            float bytesReceived = NetworkManager.instance.transport.receiveBytesPerSecond;

            
            float packetsSent = NetworkManager.instance.transport.packetsSentPerSecond;
            float packetsReceived = NetworkManager.instance.transport.packetsReceivedPerSecond;
            
            GUILayout.Label("Send : "+FormatDataRate((int)bytesSent) + " ("+packetsSent+" packets)", GUILayout.Width(300));
            GUILayout.Label("Receive : "+FormatDataRate((int)bytesReceived) + " ("+packetsReceived+" packets)", GUILayout.Width(300));
            GUILayout.Label("Packet Loss : "+NetworkManager.instance.transport.lostPacketPercentage*100+" %", GUILayout.Width(300));

            
        }
        
        public static string FormatDataRate(int bytesPerSecond)
        {
            string[] sizeSuffixes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
            int i = 0;
            double size = bytesPerSecond;

            while (size >= 1024 && i < sizeSuffixes.Length - 1)
            {
                size /= 1024;
                i++;
            }

            if (i == 0 && size < 1)
            {
                // if input is less than 1 KB/s, display in MB/s with 2 decimal places
                return $"{size*1024:0.##} {sizeSuffixes[i+1]}";
            }
            else
            {
                return $"{size:0.##} {sizeSuffixes[i]}";
            }
        }

    }
    
}