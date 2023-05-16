namespace Player.StateMachineLogic
{
    public class LandingState : State
    {
        public LandingState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void Enter()
        {
            base.Enter();
            playerController.actionComplete = false;
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.actionComplete)
            {
                ChangeState(States.Idle);
            }
        }
    }
}