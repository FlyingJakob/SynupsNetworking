using System;
using System.IO;
using SynupsNetworking.core.Enums;
using UnityEngine;

namespace SynupsNetworking.core.Misc
{
    [Serializable]
    public class Actor
    {
        public int clientID;
        public int netID;
        public int prefabID;
        public Vector3 position;
        public Vector3 rotation;

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (!(obj is Actor)) return false;

            Actor other = (Actor)obj;
            return (clientID == other.clientID) && (netID == other.netID);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + clientID.GetHashCode();
            hash = hash * 23 + netID.GetHashCode();
            return hash;
        }

    }
}