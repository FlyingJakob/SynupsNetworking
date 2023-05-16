using System;
using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class ChatMessage
{
    public string sender;
    public string message;


    public ChatMessage(string sender, string message)
    {
        this.sender = sender;
        this.message = message;
    }
}

public class SynUpsChat : NetworkCallbacks
{
    public static SynUpsChat instance;

    public List<ChatMessage> messages;
    public string userName { get; private set; }

    public bool useGUI;
    
    private void Awake()
    {
        instance = this;
    }


    public void InitChat(string playerName)
    {
        userName = playerName;
        messages.Clear();
    }

    
    public void SendChatMessage(string message)
    {
        print("sending message");

        RPC("RPCSendChatMessage", TransportChannel.Reliable, new ChatMessage(userName, message));
    }

    [SynUpsRPC]
    private void RPCSendChatMessage(ChatMessage chatMessage)
    {
        print("received message");
        messages.Insert(0,chatMessage);
        scrollPosition.y = float.MaxValue;

        if (OnReceiveMessage!=null)
        {
            OnReceiveMessage.Invoke(chatMessage);
        }
        
    }

    public UnityEvent<ChatMessage> OnReceiveMessage;
    private Vector2 scrollPosition;
    private string inputField = string.Empty;
    
    void OnGUI()
    {

        if (!useGUI)
        {
            return;
        }
        // Setup the chat window
        GUILayout.BeginArea(new Rect(10, Screen.height - 410, 400, 400));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(400), GUILayout.Height(300));

        // Draw a box behind the messages
        GUI.Box(new Rect(0, 0, 400, 300), GUIContent.none);

        // Display the messages
        foreach (ChatMessage message in messages)
        {
            GUILayout.Label($"{message.sender}: {message.message}");
        }

        GUILayout.EndScrollView();

        // Setup input field and send button
        GUILayout.BeginHorizontal();
        inputField = GUILayout.TextField(inputField, GUILayout.Width(300), GUILayout.Height(20)); // Change the height here


        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    
    
    
    
    
}
