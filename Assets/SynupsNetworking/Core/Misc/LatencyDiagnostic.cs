using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SynupsNetworking.core.Misc
{
    [Serializable]
    public class LatencyDiagnostic
    {
        public DateTime sentTimeStamp;
      
        public LatencyDiagnostic(DateTime sentTimeStamp)
        {
            this.sentTimeStamp = sentTimeStamp;
        }

        public static bool IsWithinSecond(DateTime timeStamp, out int latencyMS)
        {
            TimeSpan timeDifference = DateTime.UtcNow - timeStamp;
            latencyMS = (int)timeDifference.TotalMilliseconds;
        
            if (timeDifference.TotalSeconds > 1)
            {
                return false;
            }

            return true;
        }
    }

}