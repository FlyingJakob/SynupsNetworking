namespace Player.StateMachineLogic
{
    public class NoneUBState : State
    {
        public NoneUBState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.inputAim&&!playerController.isDead&&!playerController.blockUB)
            {
                ChangeUBState(UpperBodyStates.Aim);
            }
        }
    }
}