namespace Player.StateMachineLogic
{
    public enum States
    {
        Idle,
        Movement,
        Jump,
        InAir,
        Landing,
        Slide,
        Death,
        Sprint,
        Roll,
        Vault,
        Broom,
    }
    
    public enum UpperBodyStates
    {
        None,
        Aim,
        Movement,
        Shoot,
        Shoot2,
        Shoot3,
    }

}