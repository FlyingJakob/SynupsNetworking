using SynupsNetworking.core;
using UnityEngine;

namespace SampleGame.Scripts
{
    public class ObjectSpawnButton : MonoBehaviour
    {
        public int index;
        public void SpawnObject()
        {
            NetworkManager.instance.SpawnObject(NetworkManager.instance.networkPrefabs[index].gameObject,
                NetworkManager.instance.localPlayer.transform.position + NetworkManager.instance.localPlayer.transform.forward * 3+Vector3.up*3f,Quaternion.identity, null);
        }
    }
}