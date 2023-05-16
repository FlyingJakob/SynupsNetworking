using System.Collections;
using System.Collections.Generic;
using Player;
using SynupsNetworking.core;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyInputController : NetworkCallbacks
{
    public PlayerController enemyPlayerController;
    public float sightRange = 10f;
    public float rotationSpeed = 5f;

    public PlayerController targetPlayer;
    private bool playerInRange;
    private float randomJumpTimer;
    private float randomMoveTimer;
    private float randomStopTimer;

    public float distanceToEnemy;

    private float tc;
    
    private void Start()
    {
        FindTargetPlayer();
    }

    private bool toDestroy=false;
    
    private void Update()
    {

        if (!isMine)
        {
            return;
        }
        
        if (enemyPlayerController.isDead&&!toDestroy)
        {
            toDestroy = true;
            StartCoroutine(DestroyAfterSeconds());
            
        }

        if (enemyPlayerController.isDead)
        {
            return;
        }

        
        
        
        
        
        if (targetPlayer != null)
        {
            if (targetPlayer.isDead)
            {
                targetPlayer = null;
            }
            else
            {
                distanceToEnemy = Vector3.Distance(transform.position, targetPlayer.transform.position);
                playerInRange = distanceToEnemy <= sightRange;
            }
        }
        else
        {
            playerInRange = false;
            FindTargetPlayer();
        }

        randomJumpTimer -= Time.deltaTime;
        randomMoveTimer -= Time.deltaTime;
        randomStopTimer -= Time.deltaTime;

        tc += Time.deltaTime;
        ReadInputs();
    }

    private IEnumerator DestroyAfterSeconds()
    {
        yield return new WaitForSeconds(2f);
        NetworkManager.instance.DestroyObject(networkIdentity);
    }

    private void FindTargetPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        float closestDistance = Mathf.Infinity;
        PlayerController closestPlayer = null;

        foreach (PlayerController player in players)
        {
            if (player == enemyPlayerController || player.isDead)
            {
                continue;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            if (distanceToPlayer < closestDistance)
            {
                RaycastHit hit;
                Vector3 directionToPlayer = player.shootPos.position - transform.position;

                if (!Physics.Raycast(transform.position, directionToPlayer, out hit, distanceToPlayer,enemyPlayerController.groundedMask))
                {
                    closestDistance = distanceToPlayer;
                    closestPlayer = player;
                }
                
                
                
            }
        }

        if (closestPlayer != null)
        {
            targetPlayer = closestPlayer;
        }
        else
        {
            targetPlayer = null;
        }
    }


    private void ReadInputs()
    {
        enemyPlayerController.inputJump = randomJumpTimer <= 0 ? RandomJump() : false;
        enemyPlayerController.inputSprint = false;
        enemyPlayerController.inputAim = Mathf.PerlinNoise(7554,tc*0.4f)>0.6f;
        enemyPlayerController.inputShoot = enemyPlayerController.inputAim;
        enemyPlayerController.inputHorizontal = (Mathf.PerlinNoise(0,tc*0.05f)-0.5f)*2;
        enemyPlayerController.inputVertical = playerInRange ? (distanceToEnemy>2f?1:0): (Mathf.PerlinNoise(0,tc)-0.5f)*2;

        RotateTowardsPlayer();
    }

    private bool RandomJump()
    {
        randomJumpTimer = Random.Range(1f, 5f);
        return true;
    }

    private float RandomMove()
    {
        randomMoveTimer = Random.Range(1f, 5f);
        return Random.Range(-1f, 1f);
    }

    private float RandomStop()
    {
        randomStopTimer = Random.Range(1f, 5f);
        return 0;
    }

    private void RotateTowardsPlayer()
    {
        if (playerInRange && targetPlayer != null)
        {
            Vector3 direction = targetPlayer.transform.position - transform.position;
            direction.y = 0;

            Quaternion targetRotation;
            if (direction != Vector3.zero) 
            {
                targetRotation = Quaternion.LookRotation(direction);
            }
            else
            {
                targetRotation = transform.rotation;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            
        }
    }
}
