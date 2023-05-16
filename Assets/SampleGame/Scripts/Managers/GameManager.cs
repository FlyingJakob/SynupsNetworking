using System;
using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UMUI;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkCallbacks
{
    public static GameManager instance;
    
    public Image blockImage;

    public string playerName { get; set; } = "Player";

    private void Awake()
    {
        instance = this;
    }

    public void SetPlayerName(string name)
    {
        playerName = name;
        SynUpsChat.instance.InitChat(name);
    }

    public override void OnDisconnect()
    {
        base.OnDisconnect();

        UIManager.instance.CloseAllTabs();
        UIManager.instance.OpenTab("menu");
    }


    private void Start()
    {
        UIManager.instance.OpenTab("menu");
    }

    public void Update()
    {
        blockImage.enabled = NetworkManager.instance.localPlayer == null;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            UIManager.instance.OpenTab("chat");
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            UIManager.instance.OpenTab("objectSpawner");
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UIManager.instance.TabStack.Count==0)
            {
                UIManager.instance.OpenTab("ExitMenuTab");

            }

        }
        
        
    }

    public void KillSwitch()
    {
        TargetRPC("KillInstance",-1,TransportChannel.Reliable);

    }

    [SynUpsRPC]
    public void KillInstance()
    {
        Application.Quit();
    }

    public override void OnLocalPlayerStart()
    {
        UIManager.instance.CloseAllTabs();

    }

    public override void OnActorStart()
    {
        base.OnActorStart();
        
    }
}
