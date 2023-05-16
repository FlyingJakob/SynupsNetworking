using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using SynupsNetworking.core.Misc;
using UnityEngine;

namespace SynupsNetworking.components
{
    public class NetworkIdentity : MonoBehaviour,IComparable
    {
        internal int ownerID;
        internal int netID;
        
        internal bool isLocalPlayer;
        internal bool isMine;
        
        [Header("SyncVar")]
        [Range(0.1f,10f)]
        public float syncVarSyncRate=0.1f;
        public TransportChannel syncVarChannel;
        public bool onlySyncOnChange = true;

        internal NetworkManager networkManager;
        
        [Header("Settings")]
        public bool hasSharedAuthority=false;
        public bool dontDestroyOnDisconnect=false;


        public NetworkCallbacks[] NetCallbacks;

        private ConcurrentQueue<RPCRequest> rpcQueue = new ConcurrentQueue<RPCRequest>();

        #region Events
            public void OnStartNetworkIdentity()
            {
                if (networkManager.ProcedureDebug)
                {
                    Debug.Log("Init NetworkCallbacks");
                }
                InitNetworkCallbacks();
            }
            #endregion
        
        /// <summary>
        /// Finds the NetworkCallbacks on the gameobject and initializes them.
        /// </summary>
        private void InitNetworkCallbacks()
        {
            isMine = (ownerID != -1) && (ownerID == NetworkClient.instance.clientID);
            isLocalPlayer = networkManager.localPlayer==this;

            NetCallbacks = GetComponents<NetworkCallbacks>();
            for (int i = 0; i < NetCallbacks.Length; i++)
            {
                NetCallbacks[i].networkManager = networkManager;

                NetCallbacks[i].componentID = i;
                NetCallbacks[i].networkIdentity = this;
                NetCallbacks[i].isMine = isMine;
                NetCallbacks[i].isLocalPlayer = isLocalPlayer;
                NetCallbacks[i].OnActorStart();
                NetCallbacks[i].GetRPCMethods();
                NetCallbacks[i].GetSyncVars();
                
                //Event subscription
                networkManager.OnDisconnect.AddListener(NetCallbacks[i].OnDisconnect);
            }
        }
        private void Update()
        {
            HandleRPCQueue();

            for (int i = 0; i < NetCallbacks.Length; i++)
            {
                NetCallbacks[i].UpdateNetworkCallbacks();
            }
        }

        private void HandleRPCQueue()
        {
            if (rpcQueue.Count > 0)
            {
                int counter = 0;
                while (rpcQueue.Count > 0)
                {
                    RPCRequest request;
                    rpcQueue.TryDequeue(out request);
                    InvokeRPC(request);

                    if (counter > 20)
                    {
                        break;
                    }

                    counter++;
                }
            }
        }

        /// <summary>
        /// Helps keep the position fresh in the activeActorObjects list.
        /// </summary>
        public void UpdatePositionForActiveActorObject()
        {
            foreach (var VAR in networkManager.activeActorObjects)
            {
                if (VAR.Value == this)
                {
                    VAR.Key.position = transform.position;
                    VAR.Key.rotation = transform.rotation.eulerAngles;
                    return;
                }

            }
  
        }
        
        internal void SendRPC(ushort methodIndex,object[] parameters,int componentID,TransportChannel channel,int targetClientID)
        {
            if (networkManager.AdvancedDebug) { Debug.Log("Creating RPCRequest with index "+methodIndex);}

            RPCRequest rpcRequest = new RPCRequest(ownerID, netID, componentID, methodIndex, parameters);
            NetworkClient.instance.SendRPC(rpcRequest,channel,targetClientID);
        }

        internal void SendSyncVar(Dictionary<object,object> parameters, int componentID, TransportChannel channel)
        {
            SyncVarRequest rpcRequest = new SyncVarRequest(ownerID, netID, componentID, parameters);
            NetworkClient.instance.SendSyncVar(rpcRequest,channel);
        }
        internal void SendTargetSyncVar(Dictionary<object,object> parameters,int clientID, int componentID, TransportChannel channel)
        {
            SyncVarRequest rpcRequest = new SyncVarRequest(ownerID, netID, componentID, parameters);
            NetworkClient.instance.SendTargetSyncVar(clientID,rpcRequest,channel);
        }
        
        /// <summary>
        /// Adds an RPCRequest to the queue.
        /// </summary>
        /// <param name="rpcRequest"></param>
        public void EnqueueRPC(RPCRequest rpcRequest)
        {
            rpcQueue.Enqueue(rpcRequest);
        }
        
        public void SetSyncVars(SyncVarRequest syncVarRequest)
        {
            if (syncVarRequest.componentID>=NetCallbacks.Length)
            {
                Debug.LogWarning("WHy is this happening?");
                return;
            }
            NetworkCallbacks networkCallbacks = NetCallbacks[syncVarRequest.componentID];
            
            networkCallbacks.SetSyncVars(syncVarRequest.values);
        }

        private void InvokeRPC(RPCRequest rpcRequest)
        {
            if (rpcRequest.componentID>=NetCallbacks.Length)
            {
                Debug.LogWarning("WHy is this happening?");
                return;
            }
            NetworkCallbacks networkCallbacks = NetCallbacks[rpcRequest.componentID];
            
            networkCallbacks.InvokeRPC(rpcRequest.methodIndex,rpcRequest.parameters);
        }

        /// <summary>
        /// Transfers the ownership of the NetworkIdentity
        /// </summary>
        /// <param name="newOwner"></param>
        /// <param name="netID"></param>
        public void TransferOwnerShip(int newOwner,int netID)
        {
            this.netID = netID;
            ownerID = newOwner;
            isMine = (newOwner != -1) && (newOwner == networkManager.networkClient.clientID);
            for (int i = 0; i < NetCallbacks.Length; i++)
            {
                NetCallbacks[i].isMine = isMine;
                NetCallbacks[i].ownershipChanged = true;
            }
        }

        public int CompareTo(object obj)
        {
            return gameObject.name.CompareTo((obj as NetworkIdentity).gameObject.name);
        }
    }
}