namespace Player.StateMachineLogic
{
    public class VaultState : State
    {
        public VaultState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }

        public override void Enter()
        {
            base.Enter();
            playerController.animator.SetFloat("vaultHeight",playerController.hitHeight);
            
        }

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            playerController.ApplyVaultMovement();
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.hitHeight==-1)
            {
                ChangeState(States.Idle);
            }
        }
    }
}