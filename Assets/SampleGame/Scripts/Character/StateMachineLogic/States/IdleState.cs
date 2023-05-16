

using UnityEngine;

namespace Player.StateMachineLogic
{
    public class IdleState : GroundedState
    {
        public IdleState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void LocalUpdate()
        {
            base.Update();
            playerController.ApplyIdleMovement();
        }
        
        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (Mathf.Abs(playerController.inputHorizontal)>0f||Mathf.Abs(playerController.inputVertical)>0f)
            {
                ChangeState(States.Movement);
            }
        }
    }
}