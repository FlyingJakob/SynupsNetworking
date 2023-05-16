using System.Collections;
using System.Collections.Generic;
using UMUI;
using UnityEngine;

public class ExitMenu : UiTab
{
    
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UIManager.instance.CloseTab("ExitMenuTab");
        }
    }
}
