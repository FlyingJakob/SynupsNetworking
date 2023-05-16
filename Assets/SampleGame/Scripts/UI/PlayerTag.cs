using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTag : MonoBehaviour
{
    public Image fill;
    public Image ghostFill;

    public TextMeshProUGUI playerNameText;
    public float maxWidth;

    private float value;

    public float targetValue;

    public void SetHealth(float value)
    {
        targetValue = value;
    }

    public void SetName(string name)
    {
        playerNameText.text = name;
    }
    
    
    void Update()
    {
        
        value = Mathf.Lerp(value, targetValue / 100,
            2 * Time.deltaTime);
        
        ghostFill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0,maxWidth,value),fill.rectTransform.sizeDelta.y);
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0,maxWidth,targetValue/100),fill.rectTransform.sizeDelta.y);
    }
}
