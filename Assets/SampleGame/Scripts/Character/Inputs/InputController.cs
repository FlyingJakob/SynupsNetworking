using System;
using System.Collections;
using System.Collections.Generic;
using Player;
using SynupsNetworking.core;
using UMUI;
using UnityEngine;
using Random = UnityEngine.Random;

public class InputController : NetworkCallbacks
{
    private PlayerController playerController;
    


    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        
    }

    private void Update()
    {
        if (isMine)
        {
            //            

            if (Application.isBatchMode)
            {
                GenerateWalkingAroundInputs();
            }
            else
            {
                ReadInputs();
            }
        }
    }

    private void ReadInputs()
    {
        if (UIManager.instance.isLocked)
        {
            return;
        }
        
        playerController.inputJump = Input.GetKeyDown(KeyCode.Space);
        playerController.inputSprint = Input.GetKey(KeyCode.LeftShift);
        playerController.inputAim = Input.GetMouseButton(1);
        playerController.inputShoot = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.C);
        playerController.inputHorizontal = Input.GetAxis("Horizontal");
        playerController.inputVertical = Input.GetAxis("Vertical");
        playerController.inputRoll = Input.GetKeyDown(KeyCode.LeftControl);
    }


    private void GenerateAutoInputs()
    {
        playerController.inputSprint = false;
        playerController.inputAim = false;
        playerController.inputShoot = false;
        playerController.inputHorizontal = (Mathf.PerlinNoise(3453,tc*0.05f)-0.5f)*2;
        playerController.inputVertical = (Mathf.PerlinNoise(56756,tc*0.05f)-0.5f)*2;
        tc += Time.deltaTime;
    }

    private float tc;
    private float changeTime;
    private float randomHorizontal;
    private float randomVertical;

    private void Start()
    {
        tc = 0f;
        changeTime = 0f;
        randomHorizontal = 0f;
        randomVertical = 0f;
    }


    private IEnumerator ShootDelay()
    {
        
        playerController.inputAim = true;
        
        yield return new WaitForSeconds(Random.Range(0.1f, 1.5f));
        //playerController.inputShoot = true;
        yield return new WaitForSeconds(0.2f);
        playerController.inputShoot = false;
        playerController.inputAim = false;


    }
    
    
    private void GenerateWalkingAroundInputs()
    {
        playerController.inputSprint = false;

        tc += Time.deltaTime;

        if (tc >= changeTime)
        {
            StartCoroutine(ShootDelay());
            // Generate random values for horizontal and vertical inputs between -1 and 1
            randomHorizontal = Random.Range(-1f, 1f);
            randomVertical = Random.Range(-1f, 1f);

            // Set the next time to change the inputs
            changeTime = tc + Random.Range(1f, 3f); // Random change time between 1 and 3 seconds
        }

        // Smoothly interpolate between the current input values and the new random values
        playerController.inputHorizontal = Mathf.Lerp(playerController.inputHorizontal, randomHorizontal, Time.deltaTime);
        playerController.inputVertical = Mathf.Lerp(playerController.inputVertical, randomVertical, Time.deltaTime);
    }
    


}
