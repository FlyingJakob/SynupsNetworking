using System.Collections;
using UMUI.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UMUI.UiElements
{
    public class UMUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private bool isHovering;
        public float hoverSize = 1.2f;
        private bool isHolding;

        private bool isClicking;

        private bool usedTouch;
        
        public UnityEvent OnClick;
        public UnityEvent OnHold;
        public UnityEvent OnRelease;
        private Coroutine resizeCoroutine;

        public string clickSound;
        public string hoverSound;

        public void Resize(Vector3 newSize)
        {
            if (resizeCoroutine != null)
            {
                StopCoroutine(resizeCoroutine);
            }

            resizeCoroutine = StartCoroutine(SetSize(newSize, 20f));
        }

        private IEnumerator SetSize(Vector3 size, float speed)
        {
            while (Mathf.Abs(transform.localScale.x - size.x) > 0.01f ||
                   Mathf.Abs(transform.localScale.y - size.y) > 0.01f ||
                   Mathf.Abs(transform.localScale.z - size.z) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, size, Time.deltaTime * speed);
                yield return null;
            }
        }

        private IEnumerator ClickCoroutine()
        {
            while (Mathf.Abs(transform.localScale.x - 1f) > 0.01f || Mathf.Abs(transform.localScale.y - 1f) > 0.01f ||
                   Mathf.Abs(transform.localScale.z - 1f) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * 50f);
                yield return null;
            }

            while (Mathf.Abs(transform.localScale.x - hoverSize) > 0.01f ||
                   Mathf.Abs(transform.localScale.y - hoverSize) > 0.01f ||
                   Mathf.Abs(transform.localScale.z - hoverSize) > 0.01f)
            {
                transform.localScale =
                    Vector3.Lerp(transform.localScale, hoverSize * Vector3.one, Time.deltaTime * 50f);
                yield return null;
            }

            if (!isHovering)
            {
                while (Mathf.Abs(transform.localScale.x - 1f) > 0.01f ||
                       Mathf.Abs(transform.localScale.y - 1f) > 0.01f || Mathf.Abs(transform.localScale.z - 1f) > 0.01f)
                {
                    transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * 50f);
                    yield return null;
                }
            }
        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Input.touchCount == 0 && !usedTouch)
            {
                if (AudioManager.singleton!=null)
                {
                    AudioManager.singleton.PlayOneShot(hoverSound,false);
                }
                isHovering = true;
                Resize(hoverSize * Vector3.one);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Input.touchCount == 0)
            {
                isHovering = false;

                StopAllCoroutines();
                Resize(Vector3.one);
            }
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Input.touchCount > 0)
            {
                isHovering = false;
                Resize(hoverSize * Vector3.one);
            }

            if (Input.touchCount > 0)
            {
                usedTouch = true;
            }
            else
            {
                usedTouch = false;
            }


            isHolding = true;
            OnHold.Invoke();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (AudioManager.singleton != null)
            {
                AudioManager.singleton.PlayOneShot(clickSound,false);
            }

            StopAllCoroutines();

            resizeCoroutine = StartCoroutine(ClickCoroutine());
            OnClick.Invoke();
        }


        public void OnPointerUp(PointerEventData eventData)
        {
            StopAllCoroutines();
            Resize(Vector3.one);

            isHolding = false;
            OnRelease.Invoke();
        }
    }
}