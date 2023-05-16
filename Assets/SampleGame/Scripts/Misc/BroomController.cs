using System.Collections;
using System.Collections.Generic;
using Player;
using Player.StateMachineLogic;
using SampleGame.Scripts;
using SynupsNetworking.components;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine;

public class BroomController : InteractableNetworkCallbacks
{
    public float hoverHeight = 1.0f;
    public float hoverForce = 3;
    public float gravity = 3;
    public float tiltSpeed = 2.0f;
    public float maxGravity;
    
    public float boostSpeed = 15.0f;
    public float boostAcc;
    public float brakeAcc = 5.0f;
    public float airResistance;
    
    public float aerodynamicFactor = 0.5f;
    private CameraController cameraController;
    private CharacterController characterController;
    private float currentTiltX;
    private float currentTiltZ;
    
    private bool isBoosting = false;
    private bool isBraking = false;
    
    public bool isMounted;
    
    public Transform playerPos;
    
    
    void Start()
    {
        cameraController = Camera.main.GetComponentInParent<CameraController>();
        characterController = GetComponent<CharacterController>();
    }

    public override void OnTransferOwnership()
    {
        base.OnTransferOwnership();
        

    }

    [SyncVar]
    public string hora;
    
    [SyncVar]
    public NetworkIdentity mountedPlayer;

    public override void Interact(NetworkIdentity player)
    {
        base.Interact(player);
        player.GetComponent<PlayerController>().stateMachine.CurrentState.ChangeState(States.Broom);
        
        NetworkManager.instance.AssureOwnership(networkIdentity,NetworkClient.instance.clientID);
        //RPC("Mount",TransportChannel.Reliable,player);
        mountedPlayer = player;
        hora = "KUUK";
    }

    
    
    

    public void Mount(NetworkIdentity player)
    {
        player.transform.SetParent(playerPos);
        SetInteractable(false);
        isMounted = true;
    }
    
    public void UnMount()
    {
        if (playerPos.childCount>0)
        {
            Transform player = playerPos.GetChild(0);
            if (player!=null)
            {
                player.transform.SetParent(null);
                player.transform.eulerAngles = Vector3.zero;
            }
        }
        
        SetInteractable(true);
        isMounted = false;
    }

    public LayerMask layerMask;

    private Vector3 movedir;
    public float damp;

    void Update()
    {
        
        if (mountedPlayer!=null)
        {
            if (!isMounted)
            {
                Mount(mountedPlayer);
            }

            mountedPlayer.transform.localPosition = Vector3.Lerp(mountedPlayer.transform.localPosition, Vector3.zero,
                30 * Time.deltaTime);
            mountedPlayer.transform.localEulerAngles = Vector3.zero;
        }
        else
        {
            if (isMounted)
            {
                UnMount();
            }
        }
        
        
        if (isMine)
        {
            if (isMounted)
            {
                    RaycastHit hit;
                if (Physics.Raycast(transform.position, -Vector3.up, out hit, hoverHeight, layerMask))
                {
                    // Calculate distance to ground
                    float distanceToGround = hoverHeight - hit.distance;

                    if (distanceToGround > 0)
                    {
                        // Calculate spring force
                        float springForce = distanceToGround * hoverForce;

                        // Apply damping factor
                        float dampingForce = -damp * movedir.y;

                        // Calculate the total force
                        float totalForce = springForce + dampingForce;

                        // Limit the amplitude by reducing the total force based on the current vertical velocity
                        if (Mathf.Abs(movedir.y) > amplitudeLimit)
                        {
                            totalForce *= 0.5f; // Reduce the force (you can adjust this factor as needed)
                        }

                        // Apply total force
                        movedir.y += totalForce * Time.deltaTime;
                    }
                }
                else
                {
                    // Apply gravity
                    movedir.y -= gravity * Time.deltaTime;
                    movedir.y = Mathf.Clamp(movedir.y, -maxGravity, 100);
                }
                
                

                // Boost and brake
                isBoosting = Input.GetKey(KeyCode.LeftShift);
                isBraking = Input.GetKey(KeyCode.Space);
                
                
                float diffAngle;
                
                if (isBoosting)
                {
                    diffAngle = Vector2.SignedAngle(new Vector2(transform.forward.x,transform.forward.z), new Vector2(movedir.x,movedir.z));
                }
                else
                {
                    diffAngle = 0;
                }

                
                // Tilt broom
                float tiltDirection = Input.GetAxis("Vertical");
                currentTiltX = Mathf.Lerp(currentTiltX, tiltDirection * 45.0f, tiltSpeed * Time.deltaTime);
                currentTiltZ = Mathf.Lerp(currentTiltZ, -diffAngle, tiltSpeed * Time.deltaTime);
                
                
                
                
                transform.localEulerAngles = new Vector3(currentTiltX,transform.localEulerAngles.y,currentTiltZ);
                
                Vector3 originalRotation = transform.eulerAngles;
                
                transform.rotation = Quaternion.Lerp(transform.rotation, cameraController.transform.rotation, 20 * Time.deltaTime);
                transform.eulerAngles = new Vector3(originalRotation.x, transform.eulerAngles.y, originalRotation.z);

                

                if (isBraking)
                {
                    float y = movedir.y;
                    movedir = Vector3.Lerp(movedir, Vector3.zero, brakeAcc * Time.deltaTime);
                    movedir.y = y;
                }
                else
                {
                    if (isBoosting)
                    {
                        movedir = Vector3.Lerp(movedir, transform.forward * boostSpeed, boostAcc * Time.deltaTime);
                    }
                    else
                    {
                        float y = movedir.y;
                        movedir = Vector3.Lerp(movedir, Vector3.zero, airResistance * Time.deltaTime);
                        movedir.y = y;
                    }
                }
                MountLogic();
            }
            else
            {
                movedir = Vector3.Lerp(movedir, new Vector3(0,-2,0), 2 * Time.deltaTime);
            }
            
            characterController.Move(movedir * Time.deltaTime);

        }
        
        
        
        
    }

    public float amplitudeLimit;


    private void MountLogic()
    {
        if (Input.GetKeyDown(KeyCode.F)&&!canInteract||mountedPlayer.GetComponent<PlayerController>().isDead)
        {
            mountedPlayer.transform.position += transform.right * 1.5f;
            mountedPlayer.GetComponent<PlayerController>().stateMachine.CurrentState.ChangeState(States.Idle);
            mountedPlayer = null;
            hora = "NULL";

            //RPC("UnMount", TransportChannel.Reliable);
        }
    }

}