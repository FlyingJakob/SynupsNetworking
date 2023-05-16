using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine;

namespace SynupsNetworking.components
{
    [RequireComponent(typeof(SyncTransform))]
    public class InterestManagement : NetworkCallbacks
    {
        [SerializeField]
        private float nearDistance = 10f;
        [SerializeField]
        private float mediumDistance = 50f;

        [SerializeField]
        private float nearSyncRate = 0.1f;
        [SerializeField]
        private float mediumSyncRate = 0.5f;
        [SerializeField]
        private float farSyncRate = 1f;

        private SyncTransform syncTransform;
        

        private void Awake()
        {
            syncTransform = GetComponent<SyncTransform>();
            
        }


        private Dictionary<int, bool> canSeeMe = new Dictionary<int, bool>();

        [SynUpsRPC]
        public void SetCanSeeMe(NetworkIdentity identity,int clientID,bool state)
        {
            if (NetworkManager.instance.AdvancedDebug)
            {
                Debug.Log("SEETTTT CAN SE ME TO " + state);
            }

            if (!identity.GetComponent<InterestManagement>().canSeeMe.ContainsKey(clientID))
            {
                identity.GetComponent<InterestManagement>().canSeeMe.Add(clientID,state);
            }
            else
            {
                identity.GetComponent<InterestManagement>().canSeeMe[clientID] = state;
            }
        }
        
        

        //Run on MINE
        public float GetSyncRate(float distance, NetworkIdentity networkIdentity)
        {
            InterestManagement otherPlayer = networkIdentity.GetComponent<InterestManagement>();

            bool newCanSeeMe = IsVisibleFromCamera(networkIdentity.transform.position);

            if (!newCanSeeMe && IsVisibleFromCamera(networkIdentity.transform.position + otherPlayer.syncTransform.velocity * (mediumSyncRate * 2)))
            {
                // If the player will be inside next update
                newCanSeeMe = true;
            }

            if (!otherPlayer.canSeeMe.ContainsKey(NetworkClient.instance.clientID) || otherPlayer.canSeeMe[NetworkClient.instance.clientID] != newCanSeeMe)
            {
                TargetRPC("SetCanSeeMe", networkIdentity.ownerID, TransportChannel.Reliable, otherPlayer.GetComponent<NetworkIdentity>(), NetworkClient.instance.clientID, newCanSeeMe);
                otherPlayer.SetCanSeeMe(otherPlayer.GetComponent<NetworkIdentity>(), NetworkClient.instance.clientID, newCanSeeMe);
            }

            if (canSeeMe != null)
            {
                if (canSeeMe.ContainsKey(networkIdentity.ownerID) && !canSeeMe[networkIdentity.ownerID])
                {
                    return mediumSyncRate;
                }
            }

            if (distance <= nearDistance)
            {
                return nearSyncRate;
            }
            else if (distance <= mediumDistance)
            {
                return mediumSyncRate;
            }
            else
            {
                return farSyncRate;
            }
        }
        
        public bool IsVisibleFromCamera(Vector3 pos)
        {
            Camera cameraToUse = Camera.main;

            // Get the viewport position of the object
            Vector3 viewportPos = cameraToUse.WorldToViewportPoint(pos);

            // Check if the object is in front of the camera and within its field of view
            if (viewportPos.z > 0 && viewportPos.x > 0 && viewportPos.x < 1 && viewportPos.y > 0 && viewportPos.y < 1)
            {
                // Check if the object is occluded by any other objects in the scene
                // The object is visible
                return true;
            }
            else
            {

                // The object is not visible
                return false;
            }
        }
        

    }
}