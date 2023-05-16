using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UMUI;
using Unity.Mathematics;
using UnityEngine;

public class ChatTab : UiTab
{

    public TMP_InputField InputField;
    public ChatMessages chatMessages;
    
    
    
    public override void UpdateTab()
    {
        InputField.ActivateInputField();
        UpdateChat();
        base.UpdateTab();
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Return)&&InputField.text.Length>0)
        {
            SynUpsChat.instance.SendChatMessage(InputField.text);
            InputField.text = "";
            UIManager.instance.CloseTab(name);
            UpdateChat();
        }

        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UIManager.instance.CloseTab(name);
        }
        
    }

    public void UpdateChat()
    {
        chatMessages.UpdateChat();

    }
}
