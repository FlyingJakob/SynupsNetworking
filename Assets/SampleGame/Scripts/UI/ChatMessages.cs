using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class ChatMessages : MonoBehaviour
{
    
    public GameObject chatMessagePrefab;
    public Transform messageGrid;
    
    void Start()
    {
        
        SynUpsChat.instance.OnReceiveMessage.AddListener(AddChatMessage);
    }

    public void AddChatMessage(ChatMessage chatMessage)
    {
        UpdateChat();
        StopAllCoroutines();
        StartCoroutine(CloseChatAfterSeconds());
    }

    public IEnumerator CloseChatAfterSeconds()
    {
        yield return new WaitForSeconds(5);
        foreach (Transform child in messageGrid)
        {
            Destroy(child.gameObject);
        }
    }

    public void UpdateChat()
    {
        
        foreach (Transform child in messageGrid)
        {
            Destroy(child.gameObject);
        }

        List<ChatMessage> chatMessages = SynUpsChat.instance.messages;

        foreach (var msg in chatMessages)
        {
            GameObject obj = Instantiate(chatMessagePrefab, Vector3.zero, quaternion.identity, messageGrid);
            obj.GetComponentInChildren<TextMeshProUGUI>().text = "[" + msg.sender + "] : " + msg.message;
        }
    }

}
