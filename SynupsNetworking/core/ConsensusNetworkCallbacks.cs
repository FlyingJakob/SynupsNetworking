using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;

namespace SynupsNetworking.core
{
    /// <summary>
    /// The "ConsensusNetworkCallbacks" class extends the functionality of the "NetworkCallbacks" class by implementing the "VotedCall" method for decentralized decision-making among connected clients. The class includes methods for verifying method calls and adding votes to the voting result, as well as the "EvaluateVoting" method to calculate the voting result and execute the requested method if the voting threshold is met.
    /// </summary>
    public class ConsensusNetworkCallbacks : NetworkCallbacks
    {
        
        private Dictionary<int, List<bool>> votings = new Dictionary<int, List<bool>>();

        private int currentvotingID = 0;

        /// <summary>
        /// Executes a method on an object after receiving a sufficient number of votes from connected clients.
        /// 
        /// </summary>
        /// <param name="requestedMethodName">The name of the method to be executed.</param>
        /// <param name="verifyMethodName">The name of the verification method to check the validity of the call.</param>
        /// <param name="parameters">An array of objects representing the parameters to be passed to the method. Note that the Verify method needs to have the same parameters</param>
      
        public void VotedCall(string requestedMethodName,string verifyMethodName,params object[] parameters)
        {
            currentvotingID++;
            print("Starting voting with ID "+currentvotingID);

            StartCoroutine(VotedCallCoroutine(requestedMethodName,verifyMethodName,parameters,currentvotingID));
        }

        private IEnumerator VotedCallCoroutine(string requestedMethodName,string verifyMethodName,object[] nextParams,int votingID)
        {
            votings.Add(votingID,new List<bool>());
            TargetRPC("VoteForCall",-1,TransportChannel.Reliable,verifyMethodName,nextParams,votingID,NetworkClient.instance.clientID);

            ///TODO:Add easier access to the clientList
            while (votings[votingID].Count<NetworkClient.instance.clientList.Count-1)
            {
                yield return null;
            }
            
            bool result = EvaluateVoting(votingID);

            votings.Remove(votingID);

            if (result)
            {
                MethodInfo method = GetType().GetMethod(requestedMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                method.Invoke(this, nextParams);
            }

        }

        private bool EvaluateVoting(int votingID)
        {
            List<bool> votes = votings[votingID];

            int positive = 0;
            
            for (int i = 0; i < votes.Count; i++)
            {
                if (votes[i])
                {
                    positive++;
                }
            }

            if (votes.Count==0)
            {
                return true;
            }
            else
            {
                float percent = positive / votes.Count;

                return percent > 0.6f;
            }
            
            
            
        }
        

        [SynUpsRPC]
        private void VoteForCall(string methodName,object[] parameters,int votingID,int callerID)
        {
            MethodInfo method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool passed = (bool)method.Invoke(this, parameters);
            print("Voted "+passed+" for voting "+votingID);
            TargetRPC("AddVote",callerID,TransportChannel.Reliable,votingID,passed);
        }
        
        [SynUpsRPC]
        private void AddVote(int votingID,bool vote)
        {
            print("Added "+vote+" for voting "+votingID);
            votings[votingID].Add(vote);    
        }
        
        
        
        
        

    }
}