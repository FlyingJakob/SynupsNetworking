using UnityEngine;

namespace Player.StateMachineLogic
{
    public class RollState : State
    {
        public RollState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void Enter()
        {
            base.Enter();
            if (playerController.isMine)
            {
                playerController.actionComplete = false;
                playerController.Roll();
                playerController.blockUB = true;
                playerController.upperBodyStateMachine.CurrentState.ChangeUBState(UpperBodyStates.None);
            }
            
            
        }
        
        

        public override void Exit()
        {
            base.Exit();
            if (playerController.isMine)
            {
                playerController.blockUB = false;
            }

        }

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            playerController.ApplyRollBrakes();
            playerController.ApplyGravity();
            
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.actionComplete)
            {
                playerController.actionComplete = false;
                
                
                if (Mathf.Abs(playerController.inputHorizontal)>0f||Mathf.Abs(playerController.inputVertical)>0f)
                {
                    ChangeState(States.Movement);
                }
                else
                {
                    ChangeState(States.Idle);
                }
                
            }
        }
    }
}