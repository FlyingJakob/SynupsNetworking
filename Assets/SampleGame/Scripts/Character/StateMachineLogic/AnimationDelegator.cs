using System;
using System.Collections;
using System.Collections.Generic;
using Player;
using UnityEngine;

public class AnimationDelegator : MonoBehaviour
{
    private PlayerController playerController;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    public void SetFinishedUB()
    {
        playerController.actionCompleteUB = true;
    }
    
    public void Shoot()
    {
        playerController.shootTrigger = true;
    }
    
    
    public void SetFinished()
    {
        playerController.actionComplete = true;
    }
    
    
    
    
}
