using System.Collections;
using UnityEngine;

namespace Player.StateMachineLogic
{
    public class StateMachine
    {
        public State CurrentState { get; set; }

        
        
        public StateMachine(State InitialState)
        {
            CurrentState = InitialState;
            CurrentState.Enter();
        }
        
        public void ChangeState(State newState)
        {
            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
        
        
        
        
        
        
    }
}