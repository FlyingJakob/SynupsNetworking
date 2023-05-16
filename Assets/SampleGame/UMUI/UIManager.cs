using System;
using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using UnityEngine;
using UnityEngine.Serialization;

namespace UMUI
{
    public class UIManager : MonoBehaviour
    {

        public AnimationCurve openCurve;
        public AnimationCurve closeCurve;
        
        public static UIManager instance;
        
        public Dictionary<string, UiTab> tabs = new Dictionary<string, UiTab>();

        public List<UiTab> TabStack = new List<UiTab>();

        public bool isLocked;
        
        private void Awake()
        {
            instance = this;

            UiTab[] allTabs = Resources.FindObjectsOfTypeAll<UiTab>();

            
            
            
            foreach (var tab in allTabs)
            {
                tabs.Add(tab.name,tab);
                tab.InitTab();

               
                tab.gameObject.SetActive(false);
                
                
            }
        }

        

        public void OpenTab(string name)
        {
            
            
            
            UiTab tab = tabs[name];
            
            if (!tab.isOverlay)
            {
                foreach (var closeTab in TabStack)
                {
                    closeTab.CloseTab();
                }
            }
            else
            {
                if (TabStack.Count!=0)
                {
                    return;
                }
            }
            
            isLocked = true;
            
            
            
            
            
            
            TabStack.Add(tab);
            tab.gameObject.SetActive(true);
            tab.OpenTab();
        }

        public void CloseTopTab()
        {
            if (TabStack.Count==0)
            {
                return;
            }
            UiTab tab = TabStack[^1];
            CloseTab(TabStack.Count-1);
        }
        
        
        public void CloseAllTabs()
        {
            if (NetworkManager.instance.AdvancedDebug)
            {
                Debug.Log("Close all tabs");
            }

            foreach (var tab in TabStack)
            {
                tab.CloseTab();
            }
            TabStack.Clear();

            if (NetworkManager.instance.AdvancedDebug)
            {
                Debug.Log("Tabstack count = " + TabStack.Count);
            }

            isLocked = false;
        }

        public void CloseTab(string name) => CloseTab(tabs[name]);


        public void CloseTab(UiTab tab)
        {
            
            if (TabStack.Contains(tab))
            {
                TabStack.Remove(tab);
                tab.CloseTab();
            }

            if (!tab.isOverlay)
            {
                ShowTopTab();
            }

            if (TabStack.Count==0)
            {
                isLocked = false;
            }
            
        }


        private void CloseTab(int index)
        {
            if (index>=TabStack.Count)
            {
                return;
            }
            TabStack[index].CloseTab();
            TabStack.RemoveAt(index);
            ShowTopTab();
            
            if (TabStack.Count==0)
            {
                isLocked = false;
            }

        }

        private void ShowTopTab()
        {
            
            if (TabStack.Count==0)
            {
                return;   
            }

            UiTab tab = TabStack[^1];
            if (!tab.gameObject.activeSelf)
            {
                tab.gameObject.SetActive(true);
                tab.OpenTab();
            }
        }
    }
}

