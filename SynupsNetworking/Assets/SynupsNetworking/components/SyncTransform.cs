using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace SynupsNetworking.components
{

    
    public class SyncTransform : NetworkCallbacks
    {
        [Header("Settings")]
        public float syncRate;
        public TransportChannel channel;
        private InterestManagement interestManagement;
        public bool onlySyncOnChange = true;
        public float precision=0.1f;
        public float rotationPrecision=0.1f;
        
        [NonSerialized]
        [SyncVar]
        public bool sync =true;

        [Header("extrapolation")]
        public bool extrapolate;

        [Header("Advanced")]
        public bool extraUpdatesOnBigChanges;
        public float ForceUpdateAccelerationLimit = 1;
        
        #region Position

        private float tc;
        private Vector3 tempPos;
        private Vector3 targetPos;
        internal Vector3 velocity;
        internal Vector3 acceleration;
        private Vector3 posDeadOffset;
        private Vector3 lastPos;
        private Vector3 lastVelocity;
        private Vector3 orgPos;
        private Vector3 lastAcceleration;

        #endregion

        #region Rotation

        private Quaternion targetRotation;
        private Quaternion tempRotation;
        private Quaternion rotDeadOffset;
        private Vector3 lastRotation;

        private Vector3 angularVelocity;
        private Vector3 lastAngularVelocity;
        private Vector3 angularAcceleration;

        #endregion

        private int avgVelocitySteps=3;

        private List<Vector3> velocitySteps = new List<Vector3>();
        private List<Vector3> angularVelocitySteps = new List<Vector3>();

        private ConcurrentDictionary<int, (float,Coroutine)> players = new ConcurrentDictionary<int, (float,Coroutine)>();
        
        public float currentSyncRate { get; private set; }

        public Dictionary<int, float> syncRates = new Dictionary<int, float>();


        private Transform _transform;


        private void UpdatePlayer(int clientID,NetworkIdentity networkIdentity)
        {
            if (isMine)
            {
                
                foreach (var kvp in NetworkClient.instance.clientList)
                {
                    if (kvp.Key==NetworkClient.instance.clientID)
                    {
                        continue;
                    }

                    (float, Coroutine) output;
                    if (!players.TryGetValue(kvp.Key,out output))
                    {
                        if (players.TryAdd(kvp.Key,(syncRate,null)))
                        {
                            syncRates.Add(kvp.Key,0);
                            players[kvp.Key] = (syncRate,StartCoroutine(syncPosToClientCoroutine(kvp.Key,kvp.Value.localPlayer)));

                        }

                    }
                }
            }
            
            
        }




        private void LateUpdate()
        {
            if (!sync)
            {
                return;
            }
            if (!isMine)
            {
                ApplyReceivedPositionAndRotationData();
            }
            else
            {
                CalculatePositionAndRotationData();
            }

            tc += Time.deltaTime;
        }

        private void CalculatePositionAndRotationData()
        {
            if (extrapolate)
            {
                Vector3 currentVelocity = (transform.position - lastPos) / Time.deltaTime;


                velocitySteps.Add(currentVelocity);

                if (velocitySteps.Count > avgVelocitySteps)
                {
                    velocitySteps.RemoveAt(0);
                }

                if (angularVelocitySteps.Count > avgVelocitySteps)
                {
                    angularVelocitySteps.RemoveAt(0);
                }


                velocity = Average(velocitySteps);
                angularVelocity = Average(angularVelocitySteps);

                if (velocitySteps.Count >= 2)
                {
                    acceleration = (velocitySteps[^1] - velocitySteps[^2]) / Time.deltaTime;
                }
                else
                {
                    acceleration = Vector3.zero;
                }


                lastVelocity = velocitySteps[^1];


                if (extraUpdatesOnBigChanges)
                {
                    Vector3 accPrime = (acceleration - lastAcceleration) * Time.deltaTime;

                    if (accPrime.magnitude > ForceUpdateAccelerationLimit)
                    {
                        foreach (var kvp in players)
                        {
                            if (kvp.Key == NetworkClient.instance.clientID)
                            {
                                continue;
                            }

                            if (kvp.Value.Item1 < 0.2f)
                            {
                                TargetRPC("RPCSetPositionAndRotationExtrapolated", kvp.Key, channel, transform.position,
                                    velocity, acceleration, transform.rotation, angularVelocity, angularAcceleration, 0.1f);
                            }
                        }
                    }
                }

                angularAcceleration = (angularVelocity - lastAngularVelocity);

                lastPos = transform.position;
                lastRotation = transform.eulerAngles;
                lastAngularVelocity = angularVelocity;
                lastAcceleration = acceleration;
            }
        }

        private void ApplyReceivedPositionAndRotationData()
        {
            if (extrapolate)
            {
                tempPos = Vector3.Lerp(orgPos, targetPos, tc / (currentSyncRate == 0 ? 1 : currentSyncRate));
                posDeadOffset += velocity * Time.deltaTime;
                _transform.position = tempPos + posDeadOffset;

                tempRotation = Quaternion.Lerp(_transform.rotation, targetRotation, 10 * Time.deltaTime);
                rotDeadOffset.eulerAngles += angularVelocity * Time.deltaTime;

                Quaternion combinedRotation = tempRotation * rotDeadOffset;

                _transform.rotation = combinedRotation;
            }
            else
            {
                _transform.position = targetPos;
                _transform.rotation = targetRotation;
            }
        }



        private IEnumerator syncPosToClientCoroutine(int clientID,NetworkIdentity networkIdentity)
        {
            currentSyncRate = syncRate;

            while (!NetworkClient.instance.ClientExists(clientID))
            {
                yield return new WaitForSeconds(currentSyncRate);
            }

            if (base.networkIdentity==null)
            {
                Debug.LogError("Tried to start syncPos corotuine for nonexisting ");
            }
            
            

            Vector3 lastSentVelocity = Vector3.zero;
            Quaternion lastSentRotation = Quaternion.identity;
            
            while (networkIdentity!=null)
            {
                float distance = Vector3.Distance(networkIdentity.transform.position,
                    networkManager.localPlayer.transform.position);
                currentSyncRate = GetSyncRate(distance,networkIdentity);
                syncRates[clientID] = currentSyncRate;
                
                yield return new WaitForSeconds(currentSyncRate);

                

                float rotAngle = Quaternion.Angle(lastSentRotation, transform.rotation);
                
                if (isMine&&sync&& ((Vector3.Distance(lastSentVelocity, velocity) > precision||rotAngle>rotationPrecision)||!onlySyncOnChange))
                {
                    
                    if (extrapolate)
                    {
                        TargetRPC("RPCSetPositionAndRotationExtrapolated",clientID, channel, transform.position, velocity, acceleration, transform.rotation, angularVelocity, angularAcceleration,currentSyncRate);
                    }
                    else
                    {
                        
                        TargetRPC("RPCSetPositionAndRotation",clientID,channel,transform.position,transform.rotation);
                    }
                    lastSentVelocity = velocity;
                    lastSentRotation = transform.rotation;
                }
            }
            
            Debug.LogWarning("Local SyncTransform coroutine stopped for client "+clientID);
            
            (float, Coroutine) bajs;
            bool bajs2;
            
            players.TryRemove(clientID,out bajs);
            syncRates.Remove(clientID);

        }
        
        
        private float GetSyncRate(float distance,NetworkIdentity networkIdentity)
        {
            if (interestManagement!=null)
            {
                return interestManagement.GetSyncRate(distance,networkIdentity);
            }
            
            return syncRate;
        }


        public void Teleport(Vector3 newPosition)
        {
            RPC("RPCTeleport",channel,newPosition);
        }

        [SynUpsRPC]
        private void RPCTeleport(Vector3 newPosition)
        {
            //StopAllCoroutines();
            tc = 0;
            velocitySteps.Clear();
            angularVelocitySteps.Clear();
            velocity = Vector3.zero;
            lastPos = newPosition;
            orgPos = newPosition;
            targetPos = newPosition;
            tempPos = newPosition;
            lastRotation = newPosition;
            transform.position = newPosition;
            targetRotation = transform.rotation;
            //StartCoroutine(SyncPosCoroutine());

        }
        
        private void Start()
        {
            
            _transform = transform;
            
            if (isMine)
            {
                networkManager.OnInstantiatePlayer.AddListener(UpdatePlayer);
            }
            
            tc = 0;
            velocity = Vector3.zero;
            lastPos = transform.position;
            orgPos = transform.position;
            targetPos = transform.position;
            tempPos = transform.position;
            lastRotation = transform.eulerAngles;
            targetRotation = transform.rotation;

            if (GetComponent<InterestManagement>())
            {
                interestManagement = GetComponent<InterestManagement>();
            }


        }



        public override void OnActorStart()
        {
            if (networkManager.ProcedureDebug)
            {
                Debug.Log("SynTransform Start");
            }

            StopAllCoroutines();

            if (isMine)
            {
                UpdatePlayer(0,null);
            }

            //StartCoroutine(SyncPosCoroutine());
        }

        public override void OnTransferOwnership()
        {
            base.OnTransferOwnership();

            if (isMine)
            {
                players.Clear();
                syncRates.Clear();
                
                foreach (var kvp in NetworkClient.instance.clientList)
                {
                    if (kvp.Key==NetworkClient.instance.clientID)
                    {
                        continue;
                    }
                
                    if (!players.ContainsKey(kvp.Key))
                    {
                        players.TryAdd(kvp.Key,(syncRate,null));
                        players[kvp.Key] = (syncRate,StartCoroutine(syncPosToClientCoroutine(kvp.Key,kvp.Value.localPlayer)));
                    }
                }
                
                
            }
            else
            {
                StopAllCoroutines();
            }
        }


        [SynUpsRPC]
        public void RPCSetPositionAndRotationExtrapolated(Vector3 pos, Vector3 velocity, Vector3 acceleration, Quaternion rotation, Vector3 angularVelocity, Vector3 angularAcceleration,float receivedSyncRate)
        {
            if (isMine)
            {
                return;
            }

            currentSyncRate = receivedSyncRate;
            
            tc = 0;
            this.velocity = velocity;
            this.acceleration = acceleration;
            targetPos = pos;
            tempPos = transform.position;
            posDeadOffset = Vector3.zero;
            orgPos = transform.position;
            targetRotation = rotation;
            rotDeadOffset = Quaternion.identity;
            tempRotation = transform.rotation;
            this.angularVelocity = angularVelocity;
            this.angularAcceleration = angularAcceleration;
        }
        [SynUpsRPC]
        public void RPCSetPositionAndRotation(Vector3 pos, Quaternion rotation)
        {
            targetPos = pos;
            targetRotation = rotation;
        }
        
        
        

        private Vector3 Average(List<Vector3> vectors)
        {
            if (vectors.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            foreach (var vector in vectors)
            {
                sum += vector;
            }

            return sum / vectors.Count;
        }
    }
    
}