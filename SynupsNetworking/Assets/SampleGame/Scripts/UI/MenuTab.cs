using System;
using System.Collections;
using System.Collections.Generic;
using SampleGame.Scripts;
using SynupsNetworking.core;
using SynupsNetworking.core.Misc;
using TMPro;
using UMUI;
using UMUI.UiElements;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;


public class MenuTab : UiTab
{
    

    public Transform lobbyGrid;
    public GameObject lobbyPrefab;
    public TMP_InputField lobbyName;
    public int buttonCooldownTime = 2;
    private bool cooldown;
    public override void UpdateTab()
    {
        base.UpdateTab();
        LobbyManager.instance.RequestLobbyInformation(ReceiveLobbyInformation);
    }

    private float tc;
    
    private void Update()
    {
        /*tc += Time.deltaTime;

        if (tc>1)
        {
            LobbyManager.instance.RequestLobbyInformation(ReceiveLobbyInformation);
            tc = 0;
        }
        */
    }

    private void Start()
    {
        StartCoroutine(DisableButtonDuringCooldown());
        LobbyManager.instance.RequestLobbyInformation(ReceiveLobbyInformation);
    }

    public void UpdateLobbyList(LobbyInformation[] lobbyInformation)
    {
        foreach (Transform child in lobbyGrid)
        {
            Destroy(child.gameObject);
        }

        foreach (var li in lobbyInformation)
        {
            GameObject obj = Instantiate(lobbyPrefab, Vector3.zero, quaternion.identity, lobbyGrid);
            obj.GetComponent<LobbyItem>().lobbyInfo.text = li.lobbyName + "\n" +li.players+"/" + li.maxPlayers + "                " + li.delay;
            obj.GetComponent<LobbyItem>().lobbyName = li.lobbyName;
            obj.GetComponent<LobbyItem>().menuTab = this;
        }

    }
    
    
    public void ReceiveLobbyInformation(LobbyInformation[] lobbyInformations)
    {
        foreach (var lobbyInformation in lobbyInformations)
        {
            //print(lobbyInformation.lobbyName + " - " + lobbyInformation.players + "/" + lobbyInformation.maxPlayers);
        }
        
        UpdateLobbyList(lobbyInformations);

    }


    public void Refresh()
    {
        if (!cooldown)
        {
            LobbyManager.instance.RequestLobbyInformation(ReceiveLobbyInformation);
            cooldown = true;
        }
    }
    
    private IEnumerator DisableButtonDuringCooldown()
    {
        while (true)
        {
            if (cooldown)
            {
                cooldown = false;
            }

            yield return new WaitForSeconds(buttonCooldownTime);
        }

    }

    public void CreateLobby()
    {
        if (lobbyName.text=="")
        {
            return;
        }
        NetworkManager.instance.lobbyName = lobbyName.text;
        UIManager.instance.OpenTab("start");
    }

    public void Join(string lobbyName)
    {
        NetworkManager.instance.lobbyName = lobbyName;
        UIManager.instance.OpenTab("start");
        
    }
    
    
}


