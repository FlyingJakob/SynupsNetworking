using SynupsNetworking.core.Enums;
using UnityEngine;

namespace Player.StateMachineLogic
{
    public class State
    {
        public bool lockWeapon;
        
        protected PlayerController playerController;
        public string animName { get; set; }

        public State(PlayerController playerController, string animName)
        {
            this.playerController = playerController;
            this.animName = animName;
        }

        public void ChangeState(States state)
        {
            playerController.stateMachine.ChangeState(playerController.states[state]);
            playerController.SetState((int)state);
            //playerController.RPC("RPCSetState",TransportChannel.Reliable,state);
            
        }

        public void ChangeUBState(UpperBodyStates state)
        {
            playerController.upperBodyStateMachine.ChangeState(playerController.upperBodyStates[state]);
            playerController.nextActionQueuedUB = false;
            playerController.SetUBState((int)state);

            //playerController.RPC("RPCSetUBState",TransportChannel.Reliable,state);
        }

        public virtual void Enter()
        {
            if (animName != "")
            {
                playerController.animator.SetBool(animName, true);
            }
        }

        public virtual void Exit()
        {
            if (animName != "")
            {
                playerController.animator.SetBool(animName, false);

            }

        }

        public virtual void LocalUpdate()
        {
        }


        public virtual void Update()
        {
        }

        public virtual void FixedUpdate()
        {
        }

        public virtual void LateUpdate()
        {
        }

        public virtual void CheckForNextState()
        {
        }
    }
}