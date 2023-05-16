using UnityEngine;

namespace Player.StateMachineLogic
{
    public class MovementState : GroundedState
    {
        public MovementState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void LocalUpdate()
        {
            base.Update();
            playerController.ApplyMovement();
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (Mathf.Abs(playerController.inputHorizontal)<0.1f&&Mathf.Abs(playerController.inputVertical)<0.1f)
            {
                ChangeState(States.Idle);
            }

            if (playerController.inputSprint&&Mathf.Abs(playerController.inputHorizontal)<0.1f&&playerController.inputVertical>0.1f)
            {
                ChangeState(States.Sprint);
            }

            if (playerController.inputRoll)
            {
                ChangeState(States.Roll);
            }
        }
    }
}