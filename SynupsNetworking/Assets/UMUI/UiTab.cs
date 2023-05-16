using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace UMUI
{
    public enum TabAnimation
    {
        HorizontalScale,
        VerticalScale,
        HorizontalScaleSlerp,
        VerticalScaleSlerp,
    }

    public class UiTab : MonoBehaviour
    {
        public string name;
        public Transform tab;
        private Vector3 startSize;
        private Vector3 startPos;
        public bool isOverlay;

        public TabAnimation tabAnimation;

        public float openSpeed = 1f;
        public float closeSpeed = 1f;

        private Coroutine currentAnimationCoroutine;

        internal void InitTab()
        {
            startSize = tab.transform.localScale;
            startPos = tab.transform.position;
        }

        internal void OpenTab()
        {
            if (currentAnimationCoroutine != null)
            {
                StopCoroutine(currentAnimationCoroutine);
            }
            currentAnimationCoroutine = StartCoroutine(AnimateOpenTab());
            UpdateTab();
        }

        public virtual void UpdateTab()
        {
            
        }
        
        public void CloseTab()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }
            if (currentAnimationCoroutine != null)
            {
                StopCoroutine(currentAnimationCoroutine);
            }
            currentAnimationCoroutine = StartCoroutine(AnimateCloseTab());
        }


        private IEnumerator AnimateOpenTab()
        {
            switch (tabAnimation)
            {
                case TabAnimation.HorizontalScale:
                    yield return StartCoroutine(LerpScale(new Vector3(startSize.x, 0f, 0f), startSize,false));
                    break;
                case TabAnimation.VerticalScale:
                    yield return StartCoroutine(LerpScale(new Vector3(0f, startSize.y, 0f), startSize,false));
                    break;
                case TabAnimation.HorizontalScaleSlerp:
                    yield return StartCoroutine(SlerpScale(new Vector3(startSize.x, 0f, 0f), startSize,false));
                    break;
                case TabAnimation.VerticalScaleSlerp:
                    yield return StartCoroutine(SlerpScale(new Vector3(0f, startSize.y, 0f), startSize,false));
                    break;
            }
            currentAnimationCoroutine = null;
        }
        
        private IEnumerator AnimateCloseTab()
        {
            switch (tabAnimation)
            {
                case TabAnimation.HorizontalScale:
                    yield return StartCoroutine(LerpScale(new Vector3(startSize.x, 0f, 0f), startSize,true));
                    break;
                case TabAnimation.VerticalScale:
                    yield return StartCoroutine(LerpScale(new Vector3(0f, startSize.y, 0f), startSize,true));
                    break;
                case TabAnimation.HorizontalScaleSlerp:
                    yield return StartCoroutine(SlerpScale(new Vector3(startSize.x, 0f, 0f), startSize,true));
                    break;
                case TabAnimation.VerticalScaleSlerp:
                    yield return StartCoroutine(SlerpScale(new Vector3(0f, startSize.y, 0f), startSize,true));
                    break;
            }
            currentAnimationCoroutine = null;
            gameObject.SetActive(false);
        }


        private IEnumerator LerpScale(Vector3 startScale, Vector3 endScale,bool invert)
        {
            float t = 0f;
            while (t < 0.99f)
            {

                t = Mathf.Lerp(t, 1, Time.deltaTime * (invert?closeSpeed:openSpeed));
                
                Vector3 newScale = Vector3.Lerp(startScale, endScale,invert?1-t:t);
                tab.transform.localScale = newScale;
                yield return null;
            }
            tab.transform.localScale = endScale;
        }
        
        private IEnumerator SlerpScale(Vector3 startScale, Vector3 endScale,bool invert)
        {
            float t = 0f;
            while (t < 0.99f)
            {


                float value = (invert?UIManager.instance.closeCurve:UIManager.instance.openCurve).Evaluate(invert?1-t:t);
                
                t +=Time.deltaTime*(invert?closeSpeed:openSpeed);

                
                Vector3 newScale = Vector3.Slerp(startScale, endScale, value);
                tab.transform.localScale = newScale;
                yield return null;
            }
            tab.transform.localScale = endScale;
        }
        

        
    }
}
