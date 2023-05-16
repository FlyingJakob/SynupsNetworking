using Player.StateMachineLogic;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using SynupsNetworking.components;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine.Serialization;

namespace Player
{
    public class PlayerController : NetworkCallbacks
    {
        public StateMachine stateMachine;
        public StateMachine upperBodyStateMachine;

        public Dictionary<States, State> states;
        public Dictionary<UpperBodyStates, State> upperBodyStates;
        public Animator animator;
        
        public CharacterController controller;
        
        public Vector3 velocity;
        public Vector3 localVelocity;
        
        
        
        private Vector3 lastPos;
        private float timeCounter;

        [Header("Character Settings")] 
        [SyncVar]
        public string playerName;
        public float moveSpeed = 5f;
        public float animationMultiplier;
        public float sprintSpeed = 5f;
        public float jumpHeight = 2f;
        public float gravity = -9.81f;
        [SyncVar]
        public float health;
        public Transform groundedPos;
        public LayerMask groundedMask;
        [SyncVar]
        public bool isDead;

        [Header("Camera Settings")]
        private CameraController cameraController;
        public Transform cameraPos;
        public Vector3 aimOffset;
        public Vector3 cameraOffset;
        public Transform nameTagPosition;
        
        [Header("Inputs")] 
        public bool forceRandomInputs;
        public float inputHorizontal;
        public float inputVertical;
        public bool inputJump;
        public bool inputAim;
        public bool inputShoot;
        public bool inputSprint;
        public bool inputRoll;

        [Header("Attack")] 
        public GameObject magicBallPrefab;
        public Transform shootPos;


        [Header("Animations")] 
        public bool actionComplete;
        public bool actionCompleteUB;
        public bool blockUB;
        
        public bool nextActionQueued;
        public bool nextActionQueuedUB;
        

        [Header("VelocityLean")] 
        public float velocityLeanAmount;
        public Transform leanObj;

        public override void OnLocalPlayerStart()
        {
            base.OnLocalPlayerStart();
            playerName = GameManager.instance.playerName;

        }

        public override void OnActorStart()
        {
            base.OnActorStart();
            
            health = 100;

            if (networkIdentity.isLocalPlayer)
            {
                cameraController = Camera.main.GetComponentInParent<CameraController>();
            
                cameraController.cameraPos = cameraPos;
            }


        }

        void Start()
        {

            
            controller = GetComponent<CharacterController>();
            states = new Dictionary<States, State>
            {
                { States.Idle, new IdleState(this, animName: "idle") },
                { States.Movement, new MovementState(this, animName: "movement") },
                { States.Jump, new JumpState(this, animName: "jump") },
                { States.Landing, new LandingState(this, animName: "landing") },
                { States.InAir, new InAirState(this, animName: "inAir") },
                { States.Death, new DeathState(this, animName: "death") },
                { States.Sprint, new SprintState(this, animName: "movement") },
                { States.Roll, new RollState(this, animName: "roll") },
                { States.Vault, new VaultState(this, animName: "vault") },
                { States.Broom, new BroomState(this, animName: "broom") },
            };

            upperBodyStates = new Dictionary<UpperBodyStates, State>()
            {
                { UpperBodyStates.None, new NoneUBState(this, animName: "ub_none") },
                { UpperBodyStates.Aim, new AimUBState(this, animName: "ub_aim") },
                { UpperBodyStates.Shoot, new ShootUBState(this, animName: "ub_shoot_1") },
                { UpperBodyStates.Shoot2, new Shoot2UBState(this, animName: "ub_shoot_2") },
                { UpperBodyStates.Shoot3, new Shoot3UBState(this, animName: "ub_shoot_3") },
            };

            stateMachine = new StateMachine(states[States.Idle]);
            upperBodyStateMachine = new StateMachine(upperBodyStates[UpperBodyStates.None]);
        }

        private void SetCamera()
        {
            
            if (!isDead)
            {
                Vector3 originalRotation = transform.eulerAngles;
                transform.rotation = Quaternion.Lerp(transform.rotation, cameraController.transform.rotation, 20 * Time.deltaTime);
                transform.eulerAngles = new Vector3(originalRotation.x, transform.eulerAngles.y, originalRotation.z);
            }
            
            
            

        }


        #region Attacks

        public bool shootTrigger;

        public void Shoot(float shakeDuration,float shakeStrength,int damage)
        {
            if (isMine)
            {
                shootTrigger = false;
                
                if (networkIdentity.isLocalPlayer)
                {
                    cameraController.ShakeCamera(shakeDuration,shakeStrength);
                    
                    NetworkIdentity obj = networkManager.SpawnObject(magicBallPrefab, shootPos.position,
                        cameraController.transform.rotation,null);
                    obj.GetComponent<MagicBall>().cameraPos = Camera.main.transform.position;
                    obj.GetComponent<MagicBall>().damage = damage;
                    obj.GetComponent<MagicBall>().sender = networkIdentity;
                }
                else
                {
                    NetworkIdentity obj = networkManager.SpawnObject(magicBallPrefab, shootPos.position,
                        transform.rotation,null);
                    obj.GetComponent<MagicBall>().cameraPos = transform.position;
                    obj.GetComponent<MagicBall>().damage = damage;
                    obj.GetComponent<MagicBall>().sender = networkIdentity;
                }
                
                

            }
        }
        

        #endregion

        #region Movement

        public void Jump()
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        public void Roll()
        {

            float x = localVelocity.x;
            float z = localVelocity.z;

            if (Mathf.Abs(x)>Mathf.Abs(z))
            {
                velocity = (localVelocity.x>0?1:-1)*transform.right*8;
            }
            else
            {
                velocity = (localVelocity.z>0?1:-1)*transform.forward*8;
            }

            velocity.y = 2;

        }

        public void ApplyRollBrakes()
        {
            float yvel = velocity.y;
            velocity = Vector3.Lerp(velocity, Vector3.zero , 2 * Time.deltaTime);
            velocity.y = yvel;
        }
        
        public bool isGrounded()
        {
            bool isGrounded = Physics.CheckSphere(groundedPos.position, 0.25f, groundedMask);

            return isGrounded;
        }

        public void PushToGround()
        {
            velocity.y = -2;
        }

        public void ApplyIdleMovement()
        {
            velocity = Vector3.Lerp(velocity, Vector3.zero , 10 * Time.deltaTime);
        }

        public void ApplyMovement()
        {
            float y = velocity.y;
            Vector3 moveDirection = new Vector3(inputHorizontal, 0f, inputVertical).normalized;
            velocity = Vector3.Lerp(velocity, transform.TransformDirection(moveDirection) * moveSpeed, 10 * Time.deltaTime);
            velocity.y = y;

        }
        
        public void ApplySprintMovement()
        {
            float y = velocity.y;
            Vector3 moveDirection = new Vector3(0, 0f, inputVertical).normalized;
            velocity = Vector3.Lerp(velocity, transform.TransformDirection(moveDirection) * sprintSpeed, 10 * Time.deltaTime);
            velocity.y = y;
        }
        

        private Vector3 currentLean;
        private Vector3 lastLocalVelocity;
        private void ApplyVelocityLean()
        {

            Vector3 localAcceleration = (localVelocity-lastLocalVelocity)/(Time.deltaTime);

            Vector3 leanAmount = new Vector3(-localAcceleration.z, 0, localAcceleration.x)*(-velocityLeanAmount);

            leanAmount = Vector3.ClampMagnitude(leanAmount,1f);

            
            currentLean = Vector3.Lerp(currentLean, leanAmount, Time.deltaTime * 10f);
            


            leanObj.localEulerAngles = currentLean;
            lastLocalVelocity = localVelocity;
        }

        public void ApplyGravity()
        {
            velocity.y += gravity * Time.deltaTime;
        }

        public void ApplyDeathMovement()
        {
            ApplyGravity();
            float y = velocity.y;
            velocity = Vector3.Lerp(velocity, Vector3.zero , 10 * Time.deltaTime);
            velocity.y = y;

        }
        

        #endregion
        
        #region States


        [SyncVar]
        private int targetState;
        private int currentState;

        public void SetState(int state)
        {
            if (isMine)
            {
                targetState = state;
                currentState = state;
                return;
            }

            currentState = targetState;
            stateMachine.ChangeState(states[(States)state]);
        }

        [SyncVar]
        private int targetUBState;
        private int currentUBState;
        
        public void SetUBState(int state)
        {
            if (isMine)
            {
                targetUBState = state;
                currentUBState = state;
                return;
            }
            currentUBState = targetUBState;

            upperBodyStateMachine.ChangeState(upperBodyStates[(UpperBodyStates)state]);
        }


        public bool lockMovement;

        private void Update()
        {
            
            SetLocalVelocity();
            if (!lockMovement)
            {
                ApplyVelocityLean();
            }
            
            if (isMine)
            {
                if (isLocalPlayer)
                {
                    cameraController.cameraOffset = inputAim?aimOffset:cameraOffset;

                    if (!lockMovement)
                    {
                        SetCamera();
                    }
                }
                
                CheckHealth();
                
                CheckVault();

                

                stateMachine.CurrentState.LocalUpdate();
                stateMachine.CurrentState.CheckForNextState();

                upperBodyStateMachine.CurrentState.LocalUpdate();
                upperBodyStateMachine.CurrentState.CheckForNextState();
                
                if (!lockMovement)
                {
                    controller.Move(velocity * Time.deltaTime);
                }
            }
            else
            {
                if (currentState!=targetState)
                {
                    SetState(targetState);
                }
                
                if (currentUBState!=targetUBState)
                {
                    SetUBState(targetUBState);
                }
            }
        }
        
        

        

        private void SetLocalVelocity()
        {
            localVelocity = Vector3.Lerp(localVelocity,
                transform.InverseTransformDirection((transform.position - lastPos) / Time.deltaTime), 10 * Time.deltaTime);
            lastPos = transform.position;
            
            animator.SetFloat("velX", localVelocity.x);
            animator.SetFloat("velZ", localVelocity.z);
            
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            animator.SetFloat("speed", speed*animationMultiplier);
            
        }

        private void FixedUpdate()
        {
            stateMachine.CurrentState.FixedUpdate();
            upperBodyStateMachine.CurrentState.FixedUpdate();
        }

        private void LateUpdate()
        {
            stateMachine.CurrentState.LateUpdate();
            upperBodyStateMachine.CurrentState.LateUpdate();
        }

        

        #endregion

        #region Health

        public void Respawn()
        {
            stateMachine.CurrentState.ChangeState(States.Idle);
            health = 100;
            isDead = false;
            
            controller.enabled = false;
                    
            Vector3 newPos = networkManager.GetRandomSpawnPoint();

            transform.position = newPos;
            GetComponent<SyncTransform>().Teleport(newPos);
            controller.enabled = true;
            
        }
        
        
        private void CheckHealth()
        {
            if (health <= 0&&!isDead)
            {
                stateMachine.CurrentState.ChangeState(States.Death);
                stateMachine.CurrentState.ChangeUBState(UpperBodyStates.None);
                isDead = true;
            }
        }
        public void DamagePlayer(int i)
        {
            return;
            RPC("RPCDamagePlayer",TransportChannel.Reliable,i);
        }

        [SynUpsRPC]
        private void RPCDamagePlayer(int amount)
        {
            health -= amount;
        }

        #endregion

        #region Vault

        public int playerFit=4;
        public float vaultCheckDistance;
        public int hitHeight;
        public void CheckVault()
        {
            hitHeight = -1;
            bool[] hits = new bool[10];
            for (int i = 0; i < 10; i++)
            {
                RaycastHit hit;
                hits[i] = Physics.Raycast(transform.position + Vector3.up * i * vaultCheckDistance, transform.forward, out hit, 1,
                    groundedMask);

                if (hits[i])
                {
                    Debug.DrawLine(transform.position + Vector3.up * i * vaultCheckDistance, hit.point, Color.red, 0f);
                    
                }
            }


            int counter = 0;
            
            for (int i = 0; i < hits.Length; i++)
            {
                int index = hits.Length - i-1;

                if (!hits[index])
                {
                    counter++;
                }
                else
                {
                    if (counter>=playerFit)
                    {
                        hitHeight = index;
                    }
                    counter = 0;
                }
                
                
            }
            
            // Draw a green line for the calculated hitHeight
            if (hitHeight != -1 && hits[hitHeight])
            {
                RaycastHit hit;
                Physics.Raycast(transform.position + Vector3.up * hitHeight * vaultCheckDistance, transform.forward, out hit, 1,
                    groundedMask);
                Debug.DrawLine(transform.position + Vector3.up * hitHeight * vaultCheckDistance, hit.point, Color.green, 0f);
            }

            if (stateMachine.CurrentState is not VaultState&&hitHeight!=-1&&inputJump)
            {
                stateMachine.CurrentState.ChangeState(States.Vault);
            }
            
        }


        public void ApplyVaultMovement()
        {
            velocity = Vector3.up * 2 + transform.forward * 2;
        }

        #endregion
        

        
    }
}