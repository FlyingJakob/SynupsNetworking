using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SynupsNetworking.components;
using SynupsNetworking.core.Enums;
using SynupsNetworking.core.Misc;
using SynupsNetworking.Exceptions;
using SynupsNetworking.transport;
using UMUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace SynupsNetworking.core
{
    [Serializable]
    public class NetworkManager : MonoBehaviour
    {
        public NetworkClient networkClient{ get; private set; }
        
        [Header("Singleton")]
        public static NetworkManager instance;

        [Header("Network Objects")]
        public Transport transport;


       
        public string lobbyName { get; set; } = "TestBajs";
        public int maxPlayers;
        [FormerlySerializedAs("serverDelay")] public int rendezvousServerRTT;
        public int port;

        
        [Header("Hole Punch")]
        public string holePunchServerAddress;
        public int holePunchServerPort;

        
        [Header("Prefabs")]
        public NetworkIdentity playerPrefab;
        public List<NetworkIdentity> networkPrefabs;
        private Vector3[] spawnPoints;

        private LobbyManager lobbyManager{ get; set; }
        
        [Header("Actor lists")]
        private List<Actor> actorInstantiateQueue = new List<Actor>();
        private List<Actor> actorDestroyQueue = new List<Actor>();
        public Dictionary<Actor, NetworkIdentity> activeActorObjects{ get; private set; } = new Dictionary<Actor, NetworkIdentity>();
        public Dictionary<(int,int), NetworkIdentity> networkIdentities{ get; private set; } = new Dictionary<(int,int), NetworkIdentity>();
        private ConcurrentDictionary<int, int> failedSyncvarRequests { get; set; } = new ConcurrentDictionary<int, int>();
        private static SemaphoreSlim semaphoreOwnershipRequestActor = new SemaphoreSlim(20);
        private List<int> localActorsNetID{ get; set; } = new List<int>();

        [Header("Debug Logs")] 
        public bool PacketDebug;
        public bool PingDebug;
        public bool ProcedureDebug;
        public bool SocketDebug;
        public bool AdvancedDebug;

        [Header("Debug Stuff")] 
        public PlayerStatus playerStatus;
        public bool isConnected{ get; private set; }
        public bool forceRelay;


        public NetworkIdentity localPlayer { get; private set; }
        
        private bool InstantiateOrDestroy=true;
        
        // ConcurrentQueue to store actions to be executed on the main thread
        private ConcurrentQueue<System.Action> actionsQueue = new ConcurrentQueue<System.Action>();

        public void EnqueueAction(System.Action action)
        {
            actionsQueue.Enqueue(action);
        }
        
        #region Events

            private float updateRendezvousTC;
        
            private void Update()
            {
                while (actionsQueue.TryDequeue(out System.Action action))
                {
                    action?.Invoke();
                }
                
                
                playerStatus =networkClient?.playerStatus ?? 0;

                if (InstantiateOrDestroy)
                {
                    CheckInstantiateQueue();
                }
                else
                {
                    CheckDestroyQueue();
                }

                InstantiateOrDestroy = !InstantiateOrDestroy;

                if (networkClient!=null)
                {
                    if (networkClient.isRendezvous)
                    {
                        updateRendezvousTC += Time.deltaTime;
                        if (updateRendezvousTC>1f)
                        {
                            networkClient.SetRendezvousInLobby();
                            updateRendezvousTC = 0;
                        }
                    }
                }
                
                
            }
            
            
            
            private void Awake()
            {
                /* Init singletons */
                if(instance == null)
                   instance = this;

                lobbyManager = new LobbyManager(new IPEndPoint(IPAddress.Parse(holePunchServerAddress),holePunchServerPort));
                SynUpsSpawnPoint[] spawnPoints = FindObjectsOfType<SynUpsSpawnPoint>();
                this.spawnPoints = new Vector3[spawnPoints.Length];
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    this.spawnPoints[i] = spawnPoints[i].transform.position;
                }

            }
            private async void OnApplicationQuit()
            {
                lobbyManager.Stop();
                LobbyManager.instance = null;
                lobbyManager = null;
                
                /* Means play was clicked and then un play,
                 player does not exist in a game session, 
                 leave procedure is then unnecessary. */
                if (networkClient == null)
                {
                    return;
                }
                
                Disconnect();
            }

            internal void NewClientJoined(int clientID)
            {
                foreach (var kvp in networkIdentities)
                {
                    
                    foreach (var callback in kvp.Value.NetCallbacks)
                    {
                        if (callback.isMine)
                        {
                            callback.SendInitialSyncvarToClient(clientID);
                        }
                        //callback.SendSyncVars(false);
                        callback.OnNewClient();
                    }
                }
            }
        
        #endregion


        #region Actions

        public UnityEvent<int, NetworkIdentity> OnInstantiatePlayer { get; set; } =
            new UnityEvent<int, NetworkIdentity>();
        public UnityEvent<int, NetworkIdentity> OnDestroyPlayer { get; set; } = 
            new UnityEvent<int, NetworkIdentity>();
        
        public UnityEvent OnDisconnect { get; set; } = 
            new UnityEvent();
        

        #endregion
        
        #region Client

            public LobbyInformation GetLocalLobbyInformation()
            {
                int players = 0;

            
                foreach (var kvp in activeActorObjects)
                {
                    if (kvp.Key.prefabID==-1)
                    {
                        players++;
                    }
                }

                return new LobbyInformation(lobbyName, players, maxPlayers, rendezvousServerRTT);
            }
        
            public Vector3 GetRandomSpawnPoint()
            {
                System.Random random = new System.Random();
                
                return spawnPoints.Length==0 ? Vector3.zero : spawnPoints[random.Next(0,spawnPoints.Length)];
            }
            
            public NetworkIdentity SpawnObject(GameObject prefab,Vector3 position,Quaternion rotation,Transform parent)
            {
                NetworkIdentity networkIdentity = prefab.GetComponent<NetworkIdentity>();
                int prefabID = networkPrefabs.IndexOf(networkIdentity);
                Actor actor = new Actor()
                {
                    clientID = networkClient.clientID,
                    netID = localActorsNetID.Last() + 1,
                    prefabID = prefabID,
                    position = position,
                    rotation = rotation.eulerAngles,
                };
                
                if (actor.clientID==networkClient.clientID)
                    localActorsNetID.Add(actor.netID);
                
                networkClient.SendActor(actor);

                NetworkIdentity spawned = InstantiateLocalActor(actor);
                return spawned;
            }

            public bool DestroyObject(NetworkIdentity networkIdentity)
            {
                if (networkIdentity.ownerID!=networkClient.clientID&&!networkIdentity.hasSharedAuthority)
                {
                    return false;
                }
                DestroyGlobalActor(new Actor(){netID = networkIdentity.netID,clientID = networkIdentity.ownerID});
                return true;
            }

        
            
            /// <summary>
            /// Called from client to join a session using the rendezvousAddress and port
            /// </summary>
            public void JoinSession()
            {
                isConnected = true;
                networkClient = new NetworkClient(transport);
                transport.StartTransport(port,false);
                PingManager.instance.init();
                networkClient.Connect();
                InitStaticNetworkIdentities();
                StartCoroutine(UpdateSecond());
            }
            /// <summary>
            /// Creates a new session on the specified port
            /// </summary>
            public void CreateSession()
            {
                isConnected = true;
                networkClient = new NetworkClient(transport);
                transport.StartTransport(port,false);
                PingManager.instance.init();
                networkClient.Host();
                InitStaticNetworkIdentities();
                StartCoroutine(UpdateSecond());
            }

            private IEnumerator UpdateSecond() /* Used for pinging, heartbeat, retransmission adjustment and for fetching active actor positions in the scene */
            {
                while (true)
                {
                    /* Pinging, heartbeat, echo and adjust of retransmission.*/
                  
                    ProcessNetworkTasks();
                    
                    
                    UpdatePositionForActiveActorObjects();

                    yield return new WaitForSeconds(1f); /* One second */
                }
            }
            
            
            private void ProcessNetworkTasks()
            {
                /* Heartbeat and calculation of pings and echo. */
                networkClient.ConnectionMonitoring(); 
                
                /* Send pings simultaneously. And time stamp them. */
                networkClient.sendPings();

                /* Based on result of ping, adjust retransmission delay, to each client, appropriately. */
                (transport as UDPTransport).AdjustRetransmission();
            }

            private void UpdatePositionForActiveActorObjects()
            {
                foreach (var VAR in activeActorObjects)
                {
                    VAR.Value.UpdatePositionForActiveActorObject();
                }
            }

            public void Disconnect()
            {
                Task.Run(async () => await Leave());
                
                StartCoroutine(WaitForDisconnected());
            }

            private IEnumerator WaitForDisconnected()
            {
                while (transport.isRunning)
                {
                    yield return null;
                }
                
                DestroyAllLocalActors();
                networkIdentities.Clear();
                activeActorObjects.Clear();
                actorInstantiateQueue.Clear();
                actorDestroyQueue.Clear();
                localActorsNetID.Clear();
                failedSyncvarRequests.Clear();

                if (OnDisconnect!=null)
                {
                    OnDisconnect.Invoke();
                }
            }
            
            /// <summary>
            /// Init the leave procedure
            /// </summary>
            private async Task Leave()
            {
                isConnected = false;
                if (AdvancedDebug)
                {
                    Debug.Log("Starting disconnect Task");
                }

                await networkClient.Disconnect(); // Todo object reference not set to an instance of an object
                networkClient = null;
            }
            
      
            private void InitStaticNetworkIdentities()
            {
                NetworkIdentity[] networkIDs = FindObjectsOfType<NetworkIdentity>();

                List<NetworkIdentity> sortedList = new List<NetworkIdentity>(networkIDs);

                
                sortedList.Sort();

                networkIDs = sortedList.ToArray();

                for (int i = 0; i < networkIDs.Length; i++)
                {
                    networkIDs[i].netID = i;
                    networkIDs[i].ownerID = -1;
                    networkIDs[i].networkManager = this;
                    networkIDs[i].OnStartNetworkIdentity();
                    AddNetworkIdentity(-1,i,networkIDs[i]);
                }
                
            }
            
            
        #endregion
        
        #region ExternalActorManagement
        
            /// <summary>
            /// Instantiates an actor on every connected client.
            /// </summary>
            /// <param name="actor"></param>
            public void InstantiateGlobalActor(Actor actor)
            {
                //First instantiate it locally
                RequestInstantiateLocalActor(actor.clientID, actor.netID, actor.prefabID,actor.position,actor.rotation);
                //Then send it to everyone else
                networkClient.SendActor(actor);

            }

            private void OnStartLocalPlayer()
            {
                foreach (var networkIdentity in networkIdentities)
                {
                    if (networkIdentity.Value.ownerID==networkClient.clientID||networkIdentity.Value.hasSharedAuthority)
                    {
                        foreach (var netCallback in networkIdentity.Value.NetCallbacks)
                        {
                            netCallback.OnLocalPlayerStart();
                        }
                    }
                }
            }

            public void DestroyGlobalActor(Actor actor)
            {
                RequestDestroyLocalActor(actor);
                
                networkClient.SendDestroyActor(actor);
            }

            public void AssureOwnership(NetworkIdentity identity, int newOwnerID) 
            {
                if (identity.ownerID==newOwnerID)
                {
                    return;
                }
                TransferOwnerShip(identity);
            }

            /// <summary>
            ///  (This client will receive owner ship)
            /// </summary>
            /// <param name="clientID"></param>
            public void TransferOwnerShip(NetworkIdentity identity)
            {
                int newNetID = localActorsNetID.Last() + 1;
                networkClient.SendTransferOwnerShip(identity,networkClient.clientID,newNetID);
                TransferLocalOwnerShip(identity,networkClient.clientID,newNetID);
                localActorsNetID.Add(newNetID); 
            }

            /// <summary>
            /// Destroys a player along with all it's owned actors
            /// </summary>
            /// <param name="clientID"></param>
            public void DestroyAllLocalActorsOwnedByClient(int clientID)
            {

                Dictionary<Actor, NetworkIdentity> objs = new Dictionary<Actor, NetworkIdentity>(activeActorObjects);
                
                foreach (var kvp in objs)
                {
                    
                    if (kvp.Key.clientID!=clientID)
                    {
                        continue; 
                    }
                    
                    if (kvp.Value.dontDestroyOnDisconnect)
                    {
                        
                        int newOwner = FindNewOwner(kvp.Value);
                        
                        if (networkClient.clientID==newOwner)
                        {
                            TransferOwnerShip(kvp.Value);
                        }
                        
                    }
                    else
                    {
                        RequestDestroyLocalActor(kvp.Key);
                    }
                }
            }
            /// <summary>
            /// Finds new owner for networkIdentity
            /// </summary>
            /// <param name="networkIdentity"></param>
            /// <returns></returns>
            private int FindNewOwner(NetworkIdentity networkIdentity)
            {
                int owner = networkIdentity.ownerID;
                
                return networkClient.clientList.Keys.Min();
            }
            
            public void TransferLocalOwnerShip(NetworkIdentity networkIdentity,int newOwner,int netID)
            {
                int oldOwnerID = networkIdentity.ownerID;
                int oldNetID = networkIdentity.netID;

                Actor actor = null;

                
                //This is the worst solution ive ever made probably holy shit.
                foreach (var kvp in activeActorObjects)
                {
                    if (kvp.Key.Equals(new Actor() { clientID = oldOwnerID, netID = oldNetID }))
                    {
                        actor = kvp.Key;
                    }
                }

                
                activeActorObjects.Add(new Actor()
                {
                    clientID = newOwner,
                    netID = netID,
                    prefabID = actor.prefabID,
                    position = actor.position,
                },networkIdentity);
                
                activeActorObjects.Remove(actor);

                
                RemoveNetworkIdentity(networkIdentity.ownerID, networkIdentity.netID);
                AddNetworkIdentity(newOwner,netID,networkIdentity);
                networkIdentity.TransferOwnerShip(newOwner,netID);
            }
            
            public void DestroyAllLocalActors()
            {
                Dictionary<Actor, NetworkIdentity> temp = new Dictionary<Actor, NetworkIdentity>(activeActorObjects);
                
                foreach (var kvp in temp)
                {
                    DestroyLocalActor(kvp.Key);
                }
            }
            
            
            
        #endregion
        
        #region InternalActorManagement
        
            /// <summary>
            /// Will instantiate the actors on the instantiate queue
            /// </summary>
            private void CheckInstantiateQueue()
            {
                while (actorInstantiateQueue.Count>0)
                {
                    Actor actor = actorInstantiateQueue[0];
                    actorInstantiateQueue.RemoveAt(0);

                    
                    if (actor.clientID==networkClient.clientID)
                        localActorsNetID.Add(actor.netID);
                    
                    InstantiateLocalActor(actor);
                }
            }
            
            /// <summary>
            /// Will destroy the actors on the destroy queue
            /// </summary>
            private void CheckDestroyQueue()
            {
                while (actorDestroyQueue.Count>0)
                {
                    Actor actor = actorDestroyQueue[0];
                    actorDestroyQueue.RemoveAt(0);
                    DestroyLocalActor(actor);
                }
            }
            
            /// <summary>
            /// Adds an actor on the destroy queue. ONLY done locally
            /// </summary>
            /// <param name="actor"></param>
            public void RequestDestroyLocalActor(Actor actor)
            {
                actorDestroyQueue.Add(actor);
            }
            
            /// <summary>
            /// Adds an actor on the instantiate queue. ONLY done locally
            /// </summary>
            /// <param name="clientID"></param>
            /// <param name="netID"></param>
            /// <param name="prefabID"></param>
            internal void RequestInstantiateLocalActor(int clientID,int netID,int prefabID,Vector3 position,Vector3 rotation)
            {

                Actor newActor = new Actor()
                {
                    clientID = clientID, 
                    netID = netID, 
                    prefabID = prefabID,
                    position = position,
                    rotation = rotation,
                };

                actorInstantiateQueue.Add(newActor);
            }

            public void RequestActor(int receiverID, int netID)
            {
                byte[] data = BitConverter.GetBytes(netID);
                Packet packet = new Packet(PacketType.ActorRequest, receiverID, networkClient.clientID, data.Length, data);
                networkClient.SendPacket(packet, networkClient.GetEndpoint(receiverID), TransportChannel.Reliable);
            }
        

            /// <summary>
            /// Will instantiate the actor locally
            /// </summary>
            /// <param name="newActor"></param>
            public NetworkIdentity InstantiateLocalActor(Actor newActor)
            {
                
                GameObject obj = null;
                if (newActor.prefabID==-1)
                {
                    obj = Instantiate(playerPrefab.gameObject,newActor.position,Quaternion.Euler(newActor.rotation));
                    obj.name = "Player ID " + newActor.clientID;
                    //Invoke event
                    networkClient.clientList[newActor.clientID].localPlayer = obj.GetComponent<NetworkIdentity>();
                }
                else
                {
                    obj = Instantiate(networkPrefabs[newActor.prefabID].gameObject,newActor.position,Quaternion.Euler(newActor.rotation));
                    obj.name = networkPrefabs[newActor.prefabID].gameObject.name+" OwnerID : " + newActor.clientID;
                }

                
                obj.transform.position = newActor.position;
                obj.transform.rotation = Quaternion.Euler(newActor.rotation);
                

                NetworkIdentity networkIdentity = obj.GetComponent<NetworkIdentity>();

                if (newActor.prefabID==-1&&newActor.clientID==networkClient.clientID)
                {
                    localPlayer = networkIdentity;
                }
                
                networkIdentity.netID = newActor.netID;
                networkIdentity.ownerID = newActor.clientID;

                AddNetworkIdentity(newActor.clientID, newActor.netID, networkIdentity);
                
                networkIdentity.networkManager = this; 

                networkIdentity.OnStartNetworkIdentity();


                if (!activeActorObjects.ContainsKey(newActor))
                {
                    activeActorObjects.Add(newActor, networkIdentity);
                }
                else
                {
                    activeActorObjects.Remove(newActor);
                    activeActorObjects.Add(newActor, networkIdentity);
                }

                if (newActor.prefabID == -1)
                {
                    NewClientJoined(newActor.clientID);
                    OnInstantiatePlayer.Invoke(newActor.clientID,obj.GetComponent<NetworkIdentity>());

                }

                if (newActor.clientID==networkClient.clientID&&newActor.prefabID==-1)
                {
                    OnStartLocalPlayer();
                }
                
                return networkIdentity;
            }
            
            /// <summary>
            /// Destroys a local actor. 
            /// </summary>
            /// <param name="actor"></param>
            public void DestroyLocalActor(Actor actor)
            {
                if (!activeActorObjects.ContainsKey(actor))
                {

                    if (actorInstantiateQueue.Contains(actor))
                    {
                        actorInstantiateQueue.Remove(actor);
                    }
                    return;
                }

                Destroy(activeActorObjects[actor].gameObject);
                activeActorObjects.Remove(actor);
                RemoveNetworkIdentity(actor.clientID, actor.netID);
            }
            
        #endregion
        
        #region RPC
        
            public void InvokeLocalRPC(RPCRequest rpcRequest)
            {
                int owner = rpcRequest.clientID;

                NetworkIdentity networkIdentity = GetNetworkIdentity(rpcRequest.clientID, rpcRequest.netID);

                if (networkIdentity==null)
                {
                    return;
                }
                networkIdentity.EnqueueRPC(rpcRequest);
            }
            
     
            public async Task SetLocalSyncVars(SyncVarRequest syncVarRequest)
            {
                NetworkIdentity networkIdentity = GetNetworkIdentity(syncVarRequest.clientID, syncVarRequest.netID);
                int temp = 0;
              
                if (networkIdentity == null)
                {
                    HandleFailedSyncVar(syncVarRequest);
                    return;
                }

                networkIdentity.SetSyncVars(syncVarRequest);
            }
            #endregion
            
        #region FailedSyncVar
        
            private void HandleFailedSyncVar(SyncVarRequest syncVarRequest)
            {
                /* failedSyncvarRequests dictionary and specific boundary is used to avoid unnecessary network traffic for checking sender
               if it's component is still an existing one  (Normally, the owner, or this player, has removed the component 
               appropriately while a syncVarRequest were already sent from the owner). */
                
                /* Logs the number of failed requests for each networkIdentity */
                if (!failedSyncvarRequests.ContainsKey(syncVarRequest.netID))
                {
                    failedSyncvarRequests.TryAdd(syncVarRequest.netID, 1);
                }
                else
                {
                    failedSyncvarRequests[syncVarRequest.netID]++; /* Increments value by one. */
                }

                /* Will always be the current netIdentity (as it was increased), currently failed replies is set to 4, this could be changed later if necessary. */
                if (failedSyncvarRequests.Any(kvp => kvp.Value == 4))
                {
                    /* Remove from list as it is being handled. */
                    failedSyncvarRequests.TryRemove(syncVarRequest.netID, out int temp);
                        
                    /* This is performed separately, as creating 100 trolls and removing them at the same time necessitates an approach like this,
                     otherwise the operation, of re-adding the trolls, would be performed sequentially which would take to much time. */
                    
                    /* Also, not to allow running 1000 tasks in parallel
                     (if 10000 trolls are removed simultaneously, 10000 tasks will not be allowed in parallel 
                     mitigating the risk of crashing. 
                     */
                    
                    //Task.Run(async () => HandleOwnershipCheckAndRequestActorAsync(syncVarRequest));
                    Task.Run(async () =>
                    {
                        await semaphoreOwnershipRequestActor.WaitAsync(); 
                        try
                        {
                            await HandleOwnershipCheckAndRequestActorAsync(syncVarRequest);
                        }
                        finally
                        {
                            semaphoreOwnershipRequestActor.Release();
                        }
                    });
                    
                }

                if (failedSyncvarRequests.Count >= 1000)
                {
                    failedSyncvarRequests.Clear(); /* Check to avoid memory leak. */
                    Debug.LogWarning("Clearing failedSyncvarRequests");
                }

            }

            private async Task HandleOwnershipCheckAndRequestActorAsync(SyncVarRequest syncVarRequest)
            {
                OwnershipCheck flag = await CheckOwnershipAsync(syncVarRequest.clientID, syncVarRequest.netID);
                
                switch (flag)
                {
                    case OwnershipCheck.IsOwner:
                        /* Established that the sender is the owner and still owns the actor, request it's actor to instantiate it locally again. */
                        RequestActor(syncVarRequest.clientID, syncVarRequest.netID);
                        break;
                    case OwnershipCheck.OwnedByOthers:
                        throw new OwnershipCheckException("Received failed syncVar updates from a client, but another client is the owner of the specified actor.");
                    case OwnershipCheck.NonExistent:
                        /* Problematic if several syncVar was received but the owner does not have specific Actor. */
                        throw new OwnershipCheckException("Received failed syncVar updates from a client, but the corresponding actor does not exist.");
                    case OwnershipCheck.CheckTimedOut:
                        throw new OwnershipCheckException("Received failed syncVar updates from a client, corresponding actor couldn't be re-instantiated locally due to timeout.");
                    case OwnershipCheck.NotChecked:
                        throw new OwnershipCheckException("Received failed syncVar updates from a client, but ownership and the actor could not be determined.");
                    default:
                        throw new OwnershipCheckException("The ownership flag is corrupt.");
                }
            }

            private async Task<OwnershipCheck> CheckOwnershipAsync(int receiverID, int netID) 
            {
                /* Send packet to sender and see if it owns it. If this is the case, OwnershipCheck.IsOwner will be set. */
                OwnershipCheck flag = await networkClient.SendCheckOwnershipAsync(receiverID, netID);
                return flag;
            }

            #endregion



        #region NetworkIdentity

            public NetworkIdentity GetNetworkIdentity(int owner,int netID)
            {
                if (!networkIdentities.ContainsKey((owner,netID)))
                {
                    return null;
                }
                
                return networkIdentities[(owner, netID)];
                
            }

            public void AddNetworkIdentity(int owner,int netID,NetworkIdentity networkIdentity)
            {
                if (!networkIdentities.ContainsKey((owner, netID)))
                {
                    networkIdentities.Add((owner, netID), networkIdentity);
                }
                else
                {
                    networkIdentities.Remove((owner, netID));
                    networkIdentities.Add((owner, netID), networkIdentity);
                }
            }
            
            public void RemoveNetworkIdentity(int owner,int netID)
            {
                networkIdentities.Remove((owner,netID));
            }


        

        #endregion

        private void Start()
        {
            
            if (Application.isBatchMode)
            {
                StartCoroutine(AutoStart());
            }
        }
        private IEnumerator AutoStart()
        {
            yield return new WaitForSeconds(2);
            lobbyName = "Test";
            JoinSession();
            yield return new WaitForSeconds(1);
            UIManager.instance.CloseAllTabs();

        }
    }
}