using System.IO;
using SynupsNetworking.core.Enums;

namespace SynupsNetworking.core.Misc
{
    public class RPCRequest
    {
        public int clientID;
        public int netID;
        public int componentID;
        public ushort methodIndex;
        public object[] parameters;

        public RPCRequest(int clientID, int netID, int componentID, ushort methodIndex, object[] parameters)
        {
            this.clientID = clientID;
            this.netID = netID;
            this.componentID = componentID;
            this.methodIndex = methodIndex;
            this.parameters = parameters;
        }

        public RPCRequest()
        {
            
        }

        
        
        
    }
}