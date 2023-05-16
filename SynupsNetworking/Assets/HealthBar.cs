using System.Collections;
using System.Collections.Generic;
using Player;
using SynupsNetworking.core;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{

    public Image fill;

    public float maxWidth;

    public float value;

    void Update()
    {

        if (NetworkManager.instance.localPlayer==null)
        {
            return;
        }

        if (NetworkManager.instance.localPlayer.GetComponent<PlayerController>()==null)
        {
            return;
        }
        value = Mathf.Lerp(value, NetworkManager.instance.localPlayer.GetComponent<PlayerController>().health / 100,
            20 * Time.deltaTime);
        
        
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0,maxWidth,value),fill.rectTransform.sizeDelta.y);
        
    }
}
