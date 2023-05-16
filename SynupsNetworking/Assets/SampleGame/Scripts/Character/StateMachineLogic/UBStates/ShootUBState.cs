using Player.StateMachineLogic;

namespace Player.StateMachineLogic
{
    public class ShootUBState : State
    {
        public ShootUBState(PlayerController playerController, string animName) : base(playerController, animName)
        {
        }


        public override void Enter()
        {
            playerController.actionCompleteUB = false;
            base.Enter();
            nextQueued = false;

        }
        
        private bool nextQueued;

        public override void LocalUpdate()
        {
            base.LocalUpdate();
            if (playerController.shootTrigger)
            {
                playerController.Shoot(1,0.05f,20);
            }
        }

        public override void CheckForNextState()
        {
            base.CheckForNextState();
            
            
            
            
            
            if (playerController.inputShoot)
            {
                nextQueued = true;
            }
            
            
            if (playerController.actionCompleteUB)
            {

                
                
                if (nextQueued)
                {
                    ChangeUBState(UpperBodyStates.Shoot2);
                    return;
                }
                
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