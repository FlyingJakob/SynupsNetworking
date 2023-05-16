namespace Player.StateMachineLogic
{
    public class AimUBState : State
    {
        public AimUBState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (!playerController.inputAim)
            {
                ChangeUBState(UpperBodyStates.None);
            }

            if (playerController.inputShoot)
            {
                ChangeUBState(UpperBodyStates.Shoot);
            }
        }
    }
}