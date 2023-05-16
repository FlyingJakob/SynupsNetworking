using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using Unity.Mathematics;
using UnityEngine;

public class TestPlayer : NetworkCallbacks
{
    public List<int> received = new List<int>();

    public NetworkIdentity projectile;
    
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)&&isMine)
        {
            NetworkManager.instance.SpawnObject(projectile.gameObject, Vector3.zero, quaternion.identity, null);
        }
    }

    
}
