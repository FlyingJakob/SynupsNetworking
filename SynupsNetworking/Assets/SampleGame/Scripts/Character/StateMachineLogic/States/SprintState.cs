using UnityEngine;

namespace Player.StateMachineLogic
{
    public class SprintState : GroundedState
    {
        public SprintState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            playerController.ApplySprintMovement();
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (!playerController.inputSprint||Mathf.Abs(playerController.inputHorizontal)>0.1f)
            {
                ChangeState(States.Movement);
            }

            if (playerController.inputJump)
            {
                ChangeState(States.Jump);
            }
            
            if (playerController.inputRoll)
            {
                ChangeState(States.Roll);
            }
            
        }
    }
}