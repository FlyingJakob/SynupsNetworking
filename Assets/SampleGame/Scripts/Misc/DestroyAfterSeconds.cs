using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using UnityEngine;

public class DestroyAfterSeconds : NetworkCallbacks
{
    public float time;
    void Start()
    {
        if (isMine)
        {
            StartCoroutine(DestroyCoroutine());
        }
    }

    private IEnumerator DestroyCoroutine()
    {
        yield return new WaitForSeconds(time);
        NetworkManager.instance.DestroyObject(networkIdentity);
    }
}
