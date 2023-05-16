using System;
using System.Collections;
using SynupsNetworking.components;
using SynupsNetworking.core;
using UnityEngine;

namespace SampleGame.Scripts
{
    public class InteractableNetworkCallbacks : NetworkCallbacks
    {
        public string text;
        public KeyCode interactButton;

        public bool canInteract;
        
        

        public virtual void Interact(NetworkIdentity player)
        {
            
        }

        private Coroutine interactableCoroutine;

        public void SetInteractable(bool state)
        {
            //StopAllCoroutines();
            if (interactableCoroutine!=null)
            {
                StopCoroutine(interactableCoroutine);
            }
            interactableCoroutine = StartCoroutine(SetInteractableCoroutine(state));
        }

        private IEnumerator SetInteractableCoroutine(bool state)
        {
            yield return new WaitForSeconds(0.5f);
            canInteract = state;
        }
        
        
        
    }
}