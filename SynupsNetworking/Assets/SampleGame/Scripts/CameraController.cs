using System;
using System.Collections;
using System.Collections.Generic;
using SampleGame.Scripts;
using SynupsNetworking.core;
using TMPro;
using UMUI;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraController : MonoBehaviour
{


    public Transform cameraPos;

    public Transform camera;

    public Vector3 cameraOffset;
    public Vector3 shakeOffset;
    public float shakeFreq;
    public float cameraSensitivity;
    public LayerMask interactLM;

    public TextMeshProUGUI interactText;


    private void LateUpdate()
    {
        if (cameraPos!=null)
        {
            //transform.position = Vector3.Lerp(transform.position,cameraPos.position,20*Time.deltaTime);


            transform.position = cameraPos.position;

        }
    }

    // Update is called once per frame
    void Update()
    {

        

        if (UIManager.instance.isLocked)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        
        camera.localPosition = Vector3.Lerp(camera.localPosition,cameraOffset,20*Time.deltaTime)+shakeOffset;

        
        
        float mx = Input.GetAxis("Mouse X")*cameraSensitivity;
        float my = Input.GetAxis("Mouse Y")*cameraSensitivity;

        transform.eulerAngles += Vector3.up * mx - Vector3.right * my;
        
        //transform.Rotate(Vector3.up*mx+Vector3.right*my);

        RaycastHit hit;
        
        if (Physics.Raycast(camera.position,camera.forward,out hit,10,interactLM))
        {

            
            
            InteractableNetworkCallbacks iNC =
                hit.collider.gameObject.GetComponentInParent<InteractableNetworkCallbacks>();

            if (iNC.canInteract)
            {
                interactText.text =iNC.text+" ["+iNC.interactButton+"]";
            
                if (Input.GetKeyDown(iNC.interactButton))
                {
                    iNC.Interact(NetworkManager.instance.localPlayer);
                }
            }
            else
            {
                interactText.text = "";
            }
        }
        else
        {
            interactText.text = "";
        }
        
        
    }

    private void Start()
    {

        Application.targetFrameRate = 160;
        QualitySettings.vSyncCount = -1;
    }

    public void ShakeCamera(float duration, float strength)
    {
       StopAllCoroutines();
       StartCoroutine(ShakeCoroutine(duration, strength));
       
    }

    private IEnumerator ShakeCoroutine(float duration, float strength)
    {
        float elapsedTime = 0f;

        float randX = Random.Range(0, 1000);
        float randY = Random.Range(0, 1000);
        
        while (elapsedTime < duration)
        {
            float x = (Mathf.PerlinNoise(randX,elapsedTime*shakeFreq)-0.5f) * strength;
            float y = (Mathf.PerlinNoise(randY, elapsedTime * shakeFreq) - 0.5f) * strength;

            shakeOffset = new Vector3(x, y, 0f);
            
            elapsedTime += Time.deltaTime;
            strength = Mathf.Lerp(strength, 0f, elapsedTime / duration);
            yield return null;
        }

    }
}

