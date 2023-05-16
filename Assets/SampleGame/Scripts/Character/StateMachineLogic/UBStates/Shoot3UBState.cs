using Player.StateMachineLogic;

namespace Player.StateMachineLogic
{
    public class Shoot3UBState : State
    {
        public Shoot3UBState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }


        public override void Enter()
        {
            playerController.actionCompleteUB = false;
            base.Enter();
        }

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            if (playerController.shootTrigger)
            {
                playerController.Shoot(2,0.15f,50);

            }
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            if (playerController.actionCompleteUB)
            {

                
                if (playerController.inputAim)
                {
                    ChangeUBState(UpperBodyStates.Aim);
                }
                else
                {
                    ChangeUBState(UpperBodyStates.None);
                }
            }
        }
    }
}