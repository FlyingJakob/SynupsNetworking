using System;
using System.Reflection;
using System.Reflection.Emit;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using UnityEngine;

namespace DefaultNamespace
{
    public class TestScript : NetworkCallbacks
    {

        public GameObject projectile;

        public NetworkIdentity activeProjectile;

        public void Update()
        {

            if (isMine)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                { 
                    activeProjectile = SpawnObject(projectile,transform.position,transform.rotation);
                }
            }
            
        }


        public void Hej()
        {
        }
    }
}