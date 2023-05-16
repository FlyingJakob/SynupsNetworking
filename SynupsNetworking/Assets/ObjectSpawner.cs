using System;
using System.Collections;
using System.Collections.Generic;
using SampleGame.Scripts;
using SynupsNetworking.core;
using TMPro;
using UMUI;
using UMUI.UiElements;
using UnityEngine;

public class ObjectSpawner : UiTab
{

    public GameObject spawnObjectPrefab;

    public Transform grid;
    
    
    public override void UpdateTab()
    {
        base.UpdateTab();

        foreach (Transform child in grid)
        {
            Destroy(child.gameObject);
        }

        int counter = 0;

        foreach (var prefab in NetworkManager.instance.networkPrefabs)
        {
            GameObject obj = Instantiate(spawnObjectPrefab,Vector3.zero,Quaternion.identity,grid);
            obj.GetComponentInChildren<TextMeshProUGUI>().text = prefab.gameObject.name;
            obj.GetComponentInChildren<ObjectSpawnButton>().index = counter;
            counter++;
        }
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UIManager.instance.CloseTab("objectSpawner");
        }
    }
}
