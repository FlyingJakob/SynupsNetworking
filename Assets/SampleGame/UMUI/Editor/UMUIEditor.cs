using System;
using System.Text;
using TMPro;
using UMUI;
using UMUI.Audio;
using UMUI.UiElements;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;


public class UMUIEditor : UnityEditor.Editor
{
    [MenuItem("GameObject/UI/UMUIButton", false, 10)]
    static void CreateUMUIButton(MenuCommand menuCommand)
    {
        // Get the Canvas object
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Cannot create UMUIButton: no Canvas found in scene.");
            return;
        }

        // Create a new GameObject under the Canvas
        GameObject go = new GameObject("UMUIButton");
        go.transform.SetParent(canvas.transform, false);

        // Add an Image component to the GameObject
        Image image = go.AddComponent<Image>();
        image.color = Color.white;

        // Add the UMUIButton component to the GameObject
        UMUIButton button = go.AddComponent<UMUIButton>();

        // Add a TextMeshProUGUI child to the GameObject
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform);
        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = "Button";
        text.color = Color.black;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(160f, 30f);

        // Set the RectTransform properties of the text
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.5f);
        textRT.anchorMax = new Vector2(0.5f, 0.5f);
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textRT.anchoredPosition = Vector2.zero;

        // Set the RectTransform properties of the button
        RectTransform rectTransform = go.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(160f, 30f);

        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create UMUIButton");

        // Select the new GameObject
        Selection.activeGameObject = go;
    }
    
}
