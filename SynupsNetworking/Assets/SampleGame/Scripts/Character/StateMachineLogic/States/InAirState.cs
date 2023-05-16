using UnityEngine;

namespace Player.StateMachineLogic
{
    public class InAirState : State
    {
        public InAirState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }


        public override void LocalUpdate()
        {
            base.Update();
            playerController.ApplyGravity();

            if (playerController.velocity.y<1&&playerController.controller.velocity.y==0)
            {
                playerController.controller.Move(playerController.transform.forward * Time.deltaTime);
            }
            
            
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.isGrounded())
            {
                ChangeState(States.Idle);
            }
        }
    }
}