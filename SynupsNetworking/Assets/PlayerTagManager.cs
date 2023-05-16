using System.Collections.Generic;
using Player;
using SynupsNetworking.components;
using SynupsNetworking.core;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTagManager : MonoBehaviour
{
    public GameObject healthBarPrefab;
    public Canvas canvas;
    public float maxDistance = 50f;
    public float minSize = 0.5f;
    public float maxSize = 2f;
    private List<PlayerController> activePlayers;
    private Dictionary<PlayerController, PlayerTag> playerTags;

    void Start()
    {
        activePlayers = new List<PlayerController>();
        playerTags = new Dictionary<PlayerController, PlayerTag>();
    }


    void Update()
    {
        // Find all game objects with the PlayerController component
        PlayerController[] players = GameObject.FindObjectsOfType<PlayerController>();

        
        foreach (PlayerController player in players)
        {
            if (player.networkIdentity==null)
            {
                return;
            }
            if (player.networkIdentity.isLocalPlayer)
            {
                continue;
            }

            if (!activePlayers.Contains(player))
            {
                // Instantiate a new health bar and parent it to the canvas
                GameObject healthBarInstance = Instantiate(healthBarPrefab, canvas.transform);
                PlayerTag healthBar = healthBarInstance.GetComponent<PlayerTag>();

                // Add player and health bar to the respective lists
                activePlayers.Add(player);
                playerTags.Add(player, healthBar);
            }
        }

        Camera cam = Camera.main;
        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerController player = activePlayers[i];

            // Check if the player is still active in the scene
            if (player == null)
            {
                Destroy(playerTags[player].gameObject);
                playerTags.Remove(player);
                activePlayers.RemoveAt(i);
                i--;
            }
            else
            {
                Vector3 screenPosition = cam.WorldToScreenPoint(player.nameTagPosition.position);
                float distance = Vector3.Distance(cam.transform.position, player.transform.position);

                if (distance <= maxDistance && screenPosition.z > 0)
                {
                    // Calculate the size based on the player's distance
                    float size = Mathf.Lerp(maxSize, minSize, distance / maxDistance);
                    playerTags[player].transform.localScale = new Vector3(size, size, size);

                    playerTags[player].transform.position = screenPosition;
                    playerTags[player].SetHealth(player.health);
                    playerTags[player].SetName(player.playerName);
                    playerTags[player].gameObject.SetActive(true);
                }
                else
                {
                    playerTags[player].gameObject.SetActive(false);
                }
            }
        }
    }
}
