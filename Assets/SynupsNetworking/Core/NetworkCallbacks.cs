using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SynupsNetworking.components;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine;

namespace SynupsNetworking.core
{
    public abstract class NetworkCallbacks : MonoBehaviour
    {
        internal bool ownershipChanged;
        
        internal int componentID = -1;
        internal NetworkIdentity networkIdentity;

        internal bool isLocalPlayer;
        internal bool isMine;


        public Coroutine syncVarCoroutine;

        internal NetworkManager networkManager;

        #region GlobalEvents
            public virtual void OnLocalPlayerStart()
            {
                    
            }

            public virtual void OnDisconnect()
            {
            }
        

        #endregion
        
        #region Events

        internal void UpdateNetworkCallbacks()
        {
            if (ownershipChanged)
            {
                ownershipChanged = false;

                if (syncVarCoroutine!=null)
                {
                    StopCoroutine(syncVarCoroutine);
                }
                if (isMine)
                {
                    SendSyncVars(false);
                    syncVarCoroutine = StartCoroutine(UpdateSyncVars());
                }

                OnTransferOwnership();
            }
        }

        public virtual void OnActorStart() { }
            public virtual void OnActorStop() { }

            public virtual void OnTransferOwnership()
            {
                
            }

            public void SendSyncVars(bool onlySyncOnChange)
            {
                Dictionary<object,object> values = new Dictionary<object,object>();

                if (syncVars.Count>0)
                {
                    for (int i = 0; i < syncVars.Count; i++)
                    {
                        object value = syncVars[i].GetValue(this);

                        if (value is UnityEngine.Object unityObj && unityObj == null)
                        {
                            value = null;
                        }

                        if (!onlySyncOnChange)
                        {
                            values.Add(i,value);
                            continue;
                        }
                        
                        
                        if (value is null)
                        {
                            if (syncVarValues[i] is not null)
                            {
                                values.Add(i,value);
                            }
                            continue;
                        }
                        
                        if (!value.Equals(syncVarValues[i]))
                        {
                            values.Add(i,value);
                        }
                    }

                    if (values.Count>0)
                    {
                        networkIdentity.SendSyncVar(values,componentID,networkIdentity.syncVarChannel);
                        
                        foreach (var kvp in values)
                        {
                            syncVarValues[(int)kvp.Key] = kvp.Value;
                        }
                    }
                    
                    
                    
                }
            }

            public void SendInitialSyncvarToClient(int clientID)
            {
                Dictionary<object,object> values = new Dictionary<object,object>();

                if (syncVars.Count>0)
                {
                    for (int i = 0; i < syncVars.Count; i++)
                    {
                        object value = syncVars[i].GetValue(this);

                        if (value is UnityEngine.Object unityObj && unityObj == null)
                        {
                            value = null;
                        }
                        values.Add(i,value);
                    }

                    networkIdentity.SendTargetSyncVar(values,clientID,componentID,networkIdentity.syncVarChannel);
                    
                }
            }
            
            
            
            public virtual void OnNewClient()
            {
                
            }

        #endregion
        
        private List<FieldInfo> syncVars = new List<FieldInfo>();
        private object[] syncVarValues;

        private Dictionary<string, ushort> stringToMethodIndex = new Dictionary<string, ushort>();
        private Dictionary<ushort, MethodInfo> MethodIndexToMethodInfo = new Dictionary<ushort, MethodInfo>();
        
        
        internal IEnumerator UpdateSyncVars()
        {
            while (true)
            {
                yield return new WaitForSeconds(networkIdentity.syncVarSyncRate);
                SendSyncVars(networkIdentity.onlySyncOnChange);
            }
        }

        internal void SetSyncVars(Dictionary<object,object> values)
        {

            foreach (var kvp in values)
            {
                
                int index = (int)kvp.Key;
                if (index>=syncVars.Count)
                {
                    Debug.LogError("Something wrong with syncVar. Probably wrong netID");
                    return;
                }
                syncVars[index].SetValue(this,kvp.Value);
                if (networkManager.AdvancedDebug)
                {
                    print("SET SYNCVAR Value for " + syncVars[index].Name + " = " + kvp.Value);
                }

            }
            
        }
        
        internal void GetRPCMethods()
        {
            // Search for methods in the current type and its base type (if there is one)
            Type[] typesToSearch = (GetType().BaseType != null) ? new[] { GetType(), GetType().BaseType } : new[] { GetType() };

            MethodInfo[] rpcMethods = typesToSearch
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(m => Attribute.IsDefined(m, typeof(SynUpsRPC)))
                .ToArray();

            for (int i = 0; i < rpcMethods.Length; i++)
            {
                MethodInfo rpcMethod = rpcMethods[i];
                stringToMethodIndex[rpcMethod.Name] = ((ushort)i);
                MethodIndexToMethodInfo[((ushort)i)] = rpcMethod;
            }
        }
        internal void GetSyncVars()
        {
            Type type = GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttributes(typeof(SyncVar), true).Length > 0)
                {
                    syncVars.Add(field);
                }
            }

            syncVarValues = new object[syncVars.Count];

            if (isMine)
            {
                syncVarCoroutine = StartCoroutine(UpdateSyncVars());
            }
        }

        /// <summary>
        /// Instantiates a gameObject on all clients.
        /// </summary>
        /// <param name="prefab">The prefab to be instantiated.</param>
        /// <param name="position">The start position</param>
        /// <param name="rotation">The start rotation</param>
        /// <returns>The NetworkIdentity of the active instantiated object</returns>
        public NetworkIdentity SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
           return SpawnObject(prefab, position, rotation, null);
        }
        /// <summary>
        /// Instantiates a gameObject on all clients.
        /// </summary>
        /// <param name="prefab">The prefab to be instantiated.</param>
        /// <param name="position">The start position</param>
        /// <param name="rotation">The start rotation</param>
        /// <param name="parent">The parent to be of the new object</param>
        /// <returns>The NetworkIdentity of the active instantiated object</returns>
        public NetworkIdentity SpawnObject(GameObject prefab,Vector3 position,Quaternion rotation,Transform parent)
        {
            
           return networkManager.SpawnObject(prefab,position,rotation,parent);
        }
        /// <summary>
        /// Destroys a networkIdentity globally.
        /// </summary>
        /// <param name="networkIdentity"></param>
        public void DestroyObject(NetworkIdentity networkIdentity)
        {
            networkManager.DestroyObject(networkIdentity);
        }
        /// <summary>
        /// Makes a Remote Procedural Call (RPC) for the given method. Executes the method remotely on all connected clients.
        /// The called method is required to have the [SynUpsRPC] attribute.
        /// </summary>
        /// <param name="methodName">The name of the method to be called</param>
        /// <param name="channel">The transport channel on which the rpc will be transmitted</param>
        /// <param name="parameters">The parameters of the method call</param>
        public void RPC(string methodName,TransportChannel channel,params object[] parameters)
        {
            if (!stringToMethodIndex.ContainsKey(methodName))
            {
                Debug.LogError("RPC Method does not exist! Did you add the [SynUpsRPC] to the method?");
                return;
            }
            
            ushort methodIndex = stringToMethodIndex[methodName];
            networkIdentity.SendRPC(methodIndex,parameters,componentID,channel,-1);
            InvokeRPC(methodIndex,parameters);
        }
        /// <summary>
        /// Makes a targeted Remote Procedural Call (RPC) for the given method. This method executes the method only on one specific client.
        /// This is determined by the targetClientID. If the targetClientID = -1, it will be transmitted to everyone EXCEPT locally for the sender.
        /// </summary>
        /// <param name="methodName">The name of the method to be called</param>
        /// <param name="targetClientID">The clientID of the receiver</param>
        /// <param name="channel">The transport channel on which the rpc will be transmitted</param>
        /// <param name="parameters">The parameters of the method call</param>
        public void TargetRPC(string methodName,int targetClientID,TransportChannel channel,params object[] parameters)
        {
            if (!stringToMethodIndex.ContainsKey(methodName))
            {
                Debug.LogError("RPC Method does not exist! Did you add the [SynUpsRPC] to the method?");
                return;
            }
            ushort methodIndex = stringToMethodIndex[methodName];
            
            networkIdentity.SendRPC(methodIndex,parameters,componentID,channel,targetClientID);
            //InvokeRPC(methodIndex,parameters);
        }
        
        internal void InvokeRPC(ushort methodIndex,object[] parameters)
        {
            MethodInfo methodInfo = MethodIndexToMethodInfo[methodIndex];
            
            // Invoke the method with the parameters
            methodInfo.Invoke(this, parameters);
        }
    }
}