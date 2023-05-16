using Player;
using Player.StateMachineLogic;

namespace Player.StateMachineLogic
{
    public class GroundedState : State
    {
        public GroundedState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void LocalUpdate()
        {
            base.Update();
            playerController.PushToGround();
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.inputJump)
            {
                ChangeState(States.Jump);
            }

            if (!playerController.isGrounded())
            {
                ChangeState(States.InAir);
            }
        }
    }
}