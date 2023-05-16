using System.Collections.Generic;
using Player;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Misc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace SynupsNetworking.DebugTools
{
    public class NetworkIdentityLabels : MonoBehaviour
    {
        public GameObject labelPrefab;
        public Canvas canvas;
        public float maxDistance = 50f;
        public float minSize = 0.5f;
        public float maxSize = 2f;
        private Dictionary<NetworkIdentity, Actor> activeNetIDs;
        private Dictionary<NetworkIdentity, GameObject> labels;
        public GameObject linePrefab;
        void Start()
        {
            activeNetIDs = new Dictionary<NetworkIdentity, Actor>();
            labels = new Dictionary<NetworkIdentity, GameObject>();
        }

        void Update()
        {

            if (NetworkManager.instance.localPlayer==null)
            {
                return;
            }
            
            Camera cam = Camera.main;
            List<Vector2> usedPositions = new List<Vector2>();

            foreach (var kvp in NetworkManager.instance.activeActorObjects)
            {
                if (kvp.Value == NetworkManager.instance.localPlayer)
                {
                    continue;
                }

                if (!activeNetIDs.ContainsKey(kvp.Value))
                {
                    GameObject label = Instantiate(labelPrefab, canvas.transform);
                    activeNetIDs.Add(kvp.Value, kvp.Key);
                    labels.Add(kvp.Value, label);
                }
            }

            Dictionary<NetworkIdentity, Actor> temp = new Dictionary<NetworkIdentity, Actor>(activeNetIDs);

            foreach (var kvp in temp)
            {
                NetworkIdentity netID = kvp.Key;

                if (netID == null)
                {
                    Destroy(labels[netID].gameObject);
                    labels.Remove(netID);
                    activeNetIDs.Remove(kvp.Key);
                }
                else
                {
                    Vector3 screenPosition = cam.WorldToScreenPoint(netID.transform.position);
                    float distance = Vector3.Distance(cam.transform.position, netID.transform.position);

                    if (distance <= maxDistance && screenPosition.z > 0)
                    {
                        float size = Mathf.Lerp(maxSize, minSize, distance / maxDistance);
                        labels[netID].transform.localScale = new Vector3(size, size, size);

                        Vector2 newPosition = CalculateNonOverlappingPosition(usedPositions, screenPosition);
                        labels[netID].transform.position = newPosition;
                        usedPositions.Add(newPosition);

                        TextMeshProUGUI labelText = labels[netID].GetComponentInChildren<TextMeshProUGUI>();
                        labelText.text = "NetID : " + netID.netID + "\n" +
                                         "Owner : " + netID.ownerID + "\n" +
                                         "IsMine : " + netID.isMine + "\n" + "";

                        SyncTransform syncTransform = netID.GetComponent<SyncTransform>();

                        if (syncTransform != null)
                        {
                            labelText.text += "\nHas SyncVar\n" +
                                             "SyncRate to me: " + syncTransform.currentSyncRate + "\n" + "";
                        }

                        if (kvp.Value.prefabID == -1)
                        {
                            SyncTransform mySyncTransform = NetworkManager.instance.localPlayer.GetComponent<SyncTransform>();
                            float syncRateToThem = 999;
                            if (mySyncTransform.syncRates.ContainsKey(kvp.Key.ownerID))
                            {
                                syncRateToThem = mySyncTransform.syncRates[kvp.Key.ownerID];
                            }

                            labelText.text += "\nIs Player\n" +
                                             "SyncRate to them: " + syncRateToThem + "\n" + "";
                        }

                        labels[netID].gameObject.SetActive(true);
                        
                    }
                    else
                    {
                        labels[netID].gameObject.SetActive(false);
                    }
                }
            }
        }
        
        private Vector2 CalculateNonOverlappingPosition(List<Vector2> usedPositions, Vector3 screenPosition)
        {
            float xOffset = 150;
            float yOffset = 150;

            Vector2 newPosition = new Vector2(screenPosition.x, screenPosition.y);
            bool isOverlapping = false;

            do
            {
                isOverlapping = false;

                foreach (Vector2 usedPos in usedPositions)
                {
                    if (Vector2.Distance(newPosition, usedPos) < yOffset)
                    {
                        newPosition.y += yOffset;
                        isOverlapping = true;
                    }
                }
            } while (isOverlapping);

            return newPosition;
        }
    }
    
}