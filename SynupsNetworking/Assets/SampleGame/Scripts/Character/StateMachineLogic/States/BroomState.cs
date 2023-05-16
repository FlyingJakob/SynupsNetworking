using SynupsNetworking.components;
using UnityEngine;

namespace Player.StateMachineLogic
{
    public class BroomState : State
    {
        public BroomState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void Enter()
        {
            base.Enter();
            playerController.GetComponent<SyncTransform>().sync = false;
            playerController.velocity = Vector3.zero;
            playerController.controller.enabled = false;
            playerController.lockMovement = true;
        }

        public override void Exit()
        {
            base.Exit();
            playerController.GetComponent<SyncTransform>().sync = true;
            playerController.controller.enabled = true;

            playerController.lockMovement = false;

        }
    }
}