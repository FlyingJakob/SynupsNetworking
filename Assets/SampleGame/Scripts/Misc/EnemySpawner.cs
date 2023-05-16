using UnityEngine;
using System.Collections.Generic;
using SynupsNetworking.core;

public class EnemySpawner : ConsensusNetworkCallbacks
{
    public GameObject enemyPrefab;
    public float spawnRate = 5f; // Adjust this value to control enemy spawn rate
    public float spawnRadius = 10f; // The radius around the player where enemies can spawn
    public int targetEnemyDensity = 10; // Target enemy density within the spawn radius
    public LayerMask groundLayer; // Layer mask for the ground
    public List<Collider> spawnVolumes; // List of volumes in which the local player can spawn enemies

    private float spawnTimer;
    private List<GameObject> players;
    private List<GameObject> enemies;
    
    private void Start()
    {
        spawnTimer = 0f;
        players = new List<GameObject>();
        enemies = new List<GameObject>();
    }

    private void Update()
    {
        if (NetworkManager.instance.localPlayer==null)
        {
            return;
        }
        
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnRate && IsLocalPlayerInSpawnVolume())
        {
            SpawnEnemyIfRequired();
            spawnTimer = 0f;
        }
    }

    private void SpawnEnemyIfRequired()
    {
        enemies.Clear();
        enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        int enemiesWithinRadius = 0;
        Vector3 localPlayerPosition = NetworkManager.instance.localPlayer.transform.position;
        foreach (GameObject enemy in enemies)
        {
            if (Vector3.Distance(localPlayerPosition, enemy.transform.position) <= spawnRadius)
            {
                enemiesWithinRadius++;
            }
        }

        if (enemiesWithinRadius < targetEnemyDensity)
        {
            Vector3 spawnPosition = GetSpawnPositionFarFromPlayers();

            if (spawnPosition != Vector3.zero)
            {
                VotedCall("SpawnEnemy", "VerifySpawnEnemy", spawnPosition);
            }
        }
    }

    private Vector3 GetSpawnPositionFarFromPlayers()
    {
        players.Clear();
        players.AddRange(GameObject.FindGameObjectsWithTag("Player"));

        GameObject localPlayer = NetworkManager.instance.localPlayer.gameObject;
        Vector3 localPlayerPosition = localPlayer.transform.position;

        for (int i = 0; i < 100; i++) // Try 100 times to find a suitable spawn position
        {
            Vector3 candidatePosition = localPlayerPosition + Random.insideUnitSphere * spawnRadius;
            candidatePosition.y = 50f;
            bool farFromAllPlayers = true;
            bool isClosestPlayer = true;

            foreach (GameObject player in players)
            {
                if (player == localPlayer) continue;

                float distanceToCandidate = Vector3.Distance(candidatePosition, player.transform.position);
                if (distanceToCandidate < spawnRadius)
                {
                    farFromAllPlayers = false;
                    break;
                }
                if (Vector3.Distance(candidatePosition, localPlayerPosition) > distanceToCandidate)
                {
                    isClosestPlayer = false;
                    break;
                }
            }

            if (farFromAllPlayers && isClosestPlayer)
            {
                RaycastHit hit;
                if (Physics.Raycast(candidatePosition, Vector3.down, out hit, Mathf.Infinity, groundLayer))
                {      return hit.point + Vector3.up * 2f; // Return the position on the ground with an offset of 2 in the up direction
                }
            }
        }

        return Vector3.zero; // Return zero vector if no suitable position found
    }

    public void SpawnEnemy(Vector3 position)
    {
        print("SPAWNING ENEMY");
        NetworkManager.instance.SpawnObject(enemyPrefab, position, Quaternion.identity, transform);
    }

    public bool VerifySpawnEnemy(Vector3 position)
    {
        print("Running verification");
        return true; // You may update this to include more sophisticated verification logic
    }

    private bool IsLocalPlayerInSpawnVolume()
    {
        GameObject localPlayer = NetworkManager.instance.localPlayer.gameObject;

        foreach (Collider volume in spawnVolumes)
        {
            if (volume.bounds.Contains(localPlayer.transform.position))
            {
                return true;
            }
        }

        return false;
    }
}