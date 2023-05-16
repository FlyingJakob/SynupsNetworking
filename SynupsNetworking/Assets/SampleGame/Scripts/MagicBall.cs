using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Player;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using Unity.Mathematics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class MagicBall : NetworkCallbacks
{
    public float speed;

    private float tc = 0;

    public int damage;
    public Vector3 cameraPos;

    private Vector3 orgPos;
    
    private Vector3 posOffset;

    public NetworkIdentity sender;


    public GameObject explosion;

    public bool hasHit;

    public Light light;

    private void Start()
    {
        orgPos = transform.position;
        posOffset = Vector3.zero;
    }

    void Update()
    {
        if (isMine&&!hasHit)
        {

            orgPos = Vector3.Lerp(orgPos, cameraPos, 20 * Time.deltaTime);
            
            posOffset += transform.forward * (speed * Time.deltaTime);
            transform.position = orgPos+posOffset;
            
            
            
            tc += Time.deltaTime;
            
            
            if (tc>3)
            {
                networkManager.DestroyObject(networkIdentity);
            }
            
        }

        if (hasHit)
        {
            light.intensity = Mathf.Lerp(light.intensity, 0, 10 * Time.deltaTime);
        }
        
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!isMine)
        {
            return;
        }
        
        PlayerController hitPlayer = other.gameObject.GetComponent<PlayerController>();
        if (hitPlayer)
        {

            if (hitPlayer.networkIdentity==sender)
            {
                return;
            }
            
            
            hitPlayer.DamagePlayer(damage);
        }
        
        hasHit = true;
        
        RPC("Hit",TransportChannel.Reliable);
        

        networkManager.SpawnObject(explosion, transform.position, quaternion.identity, null);

        StartCoroutine(DestroyInSeconds(2));

    }


    [SynUpsRPC]
    public void Hit()
    {
        hasHit = true;
        GetComponentInChildren<ParticleSystem>().Stop();
        GetComponentInChildren<Collider>().enabled = false;

    }


    private IEnumerator DestroyInSeconds(float time)
    {
        yield return new WaitForSeconds(time);
        networkManager.DestroyObject(networkIdentity);
    }

}
