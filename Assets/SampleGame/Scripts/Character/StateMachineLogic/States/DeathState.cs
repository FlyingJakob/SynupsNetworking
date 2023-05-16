namespace Player.StateMachineLogic
{
    public class DeathState : State
    {
        public DeathState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        
        
        public override void CheckForNextState()
        {
            base.CheckForNextState();
            
            playerController.ApplyDeathMovement();
            
            if (playerController.inputJump)
            {
                playerController.Respawn();
            }
        }
    }
}