using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using SynupsNetworking.core;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class StatsPerClient
{
    public float totalPing;
    
    public float totalUploadThroughPut;
    public float totalDownloadThroughPut;

    public float totalSentPackets;
    public float totalReceivedPackets;
    public float totalLostPacket;
    
    public float totalFrames;
    public float avgPacketLoss;
    public float jitter;

    public int measurementsCounter;
    public float time;

    public float pingVariance;
    public float uploadThroughputVariance;
    public float downloadThroughputVariance;
    public float uploadPacketRateVariance;
    public float downloadPacketRateVariance;
    public float fpsVariance;

    public float peakUploadThroughput;
    public float peakDownloadThroughput;


    public List<float> pingValues = new List<float>();
    public List<float> uploadThroughputValues = new List<float>();
    public List<float> downloadThroughputValues = new List<float>();
    public List<float> uploadPacketRateValues = new List<float>();
    public List<float> downloadPacketRateValues = new List<float>();
    public List<float> fpsValues = new List<float>();
    
    
    #region Results

    public float pingResult;
    public float uploadThroughputResult;
    public float downloadThroughputResult;
    public float uploadPacketRateResult;
    public float downloadPacketRateResult;
    public float packetlossResult;
    public float fpsResult;

    public override string ToString()
    {
        return
            "\navg Ping : " + pingResult +
            "\navg upload Throughput : " + uploadThroughputResult +
            "\navg download Throughput : " + downloadThroughputResult +
            "\navg upload PacketRate : " + uploadPacketRateResult +
            "\navg download PacketRate : " + downloadPacketRateResult +
            "\navg FPS : " + fpsResult;
    }

    #endregion
}



public class StatisticsManager : MonoBehaviour
{

    public Dictionary<int, StatsPerClient> statsPerClients = new Dictionary<int, StatsPerClient>();

    private int connectedClientsAmount;
    
    public void AddStatistics(float sendByteCounter,float receiveByteCounter,int packetsSentCounter,int packetsReceivedCounter,float packetlossPercentage)
    {
        connectedClientsAmount = NetworkClient.instance.clientList.Count;
        

        if (!statsPerClients.ContainsKey(connectedClientsAmount))
        {
            statsPerClients.Add(connectedClientsAmount,new StatsPerClient());
        }
        StatsPerClient statsPerClient = statsPerClients[connectedClientsAmount];

        statsPerClient.totalLostPacket += packetlossPercentage;
        
        statsPerClient.totalSentPackets += packetsSentCounter;
        statsPerClient.totalReceivedPackets += packetsReceivedCounter;
        
        statsPerClient.totalDownloadThroughPut += receiveByteCounter;
        statsPerClient.totalUploadThroughPut += sendByteCounter;

        if (sendByteCounter*2>statsPerClient.peakUploadThroughput)
        {
            statsPerClient.peakUploadThroughput = sendByteCounter*2;
        }
        
        if (receiveByteCounter*2>statsPerClient.peakDownloadThroughput)
        {
            statsPerClient.peakDownloadThroughput = receiveByteCounter*2;
        }
        
        
        

        float avgPing = 0;
        int counter = 0;

        foreach (var kvp in  NetworkClient.instance.clientList)
        {
            if (kvp.Key != NetworkClient.instance.clientID)
            {
                if (kvp.Value.ping!=999)
                {
                    avgPing += kvp.Value.ping;
                    counter++;
                }
            }
        }
        

        avgPing /= counter;

        statsPerClient.totalPing += avgPing;
        
        statsPerClient.measurementsCounter++;
        statsPerClient.time+=0.5f;
        
        
        
        statsPerClient.pingValues.Add(avgPing);
        statsPerClient.uploadThroughputValues.Add(sendByteCounter);
        statsPerClient.downloadThroughputValues.Add(receiveByteCounter);
        statsPerClient.uploadPacketRateValues.Add(packetsSentCounter);
        statsPerClient.downloadPacketRateValues.Add(packetsReceivedCounter);

        
        
    }

    private void Update()
    {
        
        if (statsPerClients.ContainsKey(connectedClientsAmount))
        {
            statsPerClients[connectedClientsAmount].totalFrames++;

        }

        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            //Debug.Log(EvaluateStatistics(1).ToString());
            WriteFile();
            Debug.Log("Saved Statistics");
        }
    }

    public void WriteFile()
    {
        string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SynUpsLog.csv");
        string textToWrite = GenerateCSV();

        
        File.WriteAllText(filePath, textToWrite);
        //Process.Start("explorer.exe", "/select," + filePath);
    }
    

    public StatsPerClient EvaluateStatistics(int connectedClients)
    {
        StatsPerClient statsPerClient = statsPerClients[connectedClients];

        float time = statsPerClient.time;
        int iterations = statsPerClient.measurementsCounter;

        statsPerClient.fpsResult = statsPerClient.totalFrames / time;
        statsPerClient.pingResult = statsPerClient.totalPing / iterations;

        statsPerClient.downloadThroughputResult = statsPerClient.totalDownloadThroughPut / time;
        statsPerClient.uploadThroughputResult = statsPerClient.totalUploadThroughPut / time;
        
        statsPerClient.downloadPacketRateResult = statsPerClient.totalReceivedPackets / time;
        statsPerClient.uploadPacketRateResult = statsPerClient.totalSentPackets / time;
        statsPerClient.packetlossResult = statsPerClient.totalLostPacket / iterations;
        CalculateVariance(connectedClients);
        
        return statsPerClient;
    }
    
    public string GenerateCSV()
    {
        StringBuilder csv = new StringBuilder();

        // Header
        csv.AppendLine("Clients,PingResult,PingVariance,UploadThroughput,UploadThroughputVariance,UploadThroughputPeak,DownloadThroughput,DownloadThroughputVariance,DownloadThroughputPeak,UploadPacketRate,UploadPacketRateVariance,DownloadPacketRate,DownloadPacketRateVariance,FPS,FPSVariance,PacketLoss");

// Data rows
        foreach (var kvp in statsPerClients)
        {
            EvaluateStatistics(kvp.Key);
            csv.AppendLine($"{kvp.Key},{kvp.Value.pingResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.pingVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.uploadThroughputResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.uploadThroughputVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.peakUploadThroughput.ToString(CultureInfo.InvariantCulture)},{kvp.Value.downloadThroughputResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.downloadThroughputVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.peakDownloadThroughput.ToString(CultureInfo.InvariantCulture)},{kvp.Value.uploadPacketRateResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.uploadPacketRateVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.downloadPacketRateResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.downloadPacketRateVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.fpsResult.ToString(CultureInfo.InvariantCulture)},{kvp.Value.fpsVariance.ToString(CultureInfo.InvariantCulture)},{kvp.Value.packetlossResult.ToString(CultureInfo.InvariantCulture)}");
        }

        return csv.ToString();
    }

    
    private void CalculateVariance(int connectedClients)
    {
        StatsPerClient statsPerClient = statsPerClients[connectedClients];

        statsPerClient.pingVariance = CalculateVariance(statsPerClient.pingValues, statsPerClient.pingResult);
        statsPerClient.uploadThroughputVariance = CalculateVariance(statsPerClient.uploadThroughputValues, statsPerClient.uploadThroughputResult);
        statsPerClient.downloadThroughputVariance = CalculateVariance(statsPerClient.downloadThroughputValues, statsPerClient.downloadThroughputResult);
        statsPerClient.uploadPacketRateVariance = CalculateVariance(statsPerClient.uploadPacketRateValues, statsPerClient.uploadPacketRateResult);
        statsPerClient.downloadPacketRateVariance = CalculateVariance(statsPerClient.downloadPacketRateValues, statsPerClient.downloadPacketRateResult);
        statsPerClient.fpsVariance = CalculateVariance(statsPerClient.fpsValues, statsPerClient.fpsResult);
    }

    private float CalculateVariance(List<float> values, float mean)
    {
        float sumOfSquaredDifferences = 0;
        foreach (float value in values)
        {
            sumOfSquaredDifferences += (value - mean) * (value - mean);
        }
        return sumOfSquaredDifferences / (values.Count - 1);
    }


}
