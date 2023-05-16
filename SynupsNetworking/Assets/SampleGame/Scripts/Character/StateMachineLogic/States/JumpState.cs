using Player;
using Player.StateMachineLogic;

namespace Player.StateMachineLogic
{
    public class JumpState : State
    {
        public JumpState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }


        public override void Enter()
        {
            base.Enter();
            playerController.Jump();
        }

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            playerController.ApplyGravity();
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.velocity.y<0)
            {
                ChangeState(States.InAir);
            }

            if (playerController.inputRoll)
            {
                ChangeState(States.Roll);
            }
        }
    }
}