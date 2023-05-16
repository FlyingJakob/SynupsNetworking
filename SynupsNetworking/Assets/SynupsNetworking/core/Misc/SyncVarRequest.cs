using System.Collections.Generic;

namespace SynupsNetworking.core.Misc
{
    public class SyncVarRequest
    {
        public int clientID;
        public int netID;
        public int componentID;
        public Dictionary<object,object> values;

        public SyncVarRequest(int clientID, int netID, int componentID, Dictionary<object,object> values)
        {
            this.clientID = clientID;
            this.netID = netID;
            this.componentID = componentID;
            this.values = values;
        }

        public SyncVarRequest()
        {
            
        }
    }
}