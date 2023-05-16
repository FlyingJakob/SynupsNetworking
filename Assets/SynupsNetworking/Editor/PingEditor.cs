using SynupsNetworking.core;
using SynupsNetworking.core.Misc;
using UnityEditor;
using UnityEngine;

namespace SynupsNetworking
{  
    [CustomEditor(typeof(PingManager))]  
    public class PingEditor : Editor  
    {  
        public override void OnInspectorGUI()  
        {     
            PingManager pingManager = (PingManager)target;  
            
            EditorGUILayout.LabelField("Ping Settings", EditorStyles.boldLabel);
            
            pingManager.showRepliesCheckBox = EditorGUILayout.Toggle("Show replies", pingManager.showRepliesCheckBox);

            EditorGUI.BeginChangeCheck(); /* Checks if any changes are made */
            pingManager.defaultValueCheckBox = EditorGUILayout.Toggle("Default values", pingManager.defaultValueCheckBox);
            if (EditorGUI.EndChangeCheck())
            {
                 Undo.RecordObject(pingManager, "Changed checkbox default value. ");
                 pingManager.DefaultValueCheck();
            }

            EditorGUI.BeginDisabledGroup(pingManager.defaultValueCheckBox);
            pingManager.pingCountField = EditorGUILayout.IntField("Ping count", pingManager.pingCountField);
            pingManager.bytesField = EditorGUILayout.IntField("Bytes 20+", pingManager.bytesField);
            pingManager.replyTimeoutMSField = EditorGUILayout.IntField("Reply timeout (ms)", pingManager.replyTimeoutMSField);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginChangeCheck();      
            pingManager.advancedValueCheckBox = EditorGUILayout.Toggle("Advanced", pingManager.advancedValueCheckBox);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pingManager, "Changed checkbox advanced. ");
                pingManager.AdvancedCheck();
            }

            EditorGUI.BeginDisabledGroup(!pingManager.advancedValueCheckBox);
            pingManager.fixedTransmissionDelay = EditorGUILayout.IntField("Transmission delay (ms)", pingManager.fixedTransmissionDelay);
            EditorGUI.EndDisabledGroup();
            
            // -----------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(pingManager.useSocketCheckBox);
            pingManager.receivingClientIDField = EditorGUILayout.IntField("ClientID", pingManager.receivingClientIDField);
            EditorGUI.EndDisabledGroup();
            // -----------------------------------------
            
            EditorGUI.BeginChangeCheck();
            pingManager.useSocketCheckBox = EditorGUILayout.Toggle("Socket", pingManager.useSocketCheckBox);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pingManager, "Changed checkbox for socket address. "); // this does what?
                
            }
            
            EditorGUI.BeginDisabledGroup(!pingManager.useSocketCheckBox);
            pingManager.AddressField = EditorGUILayout.TextField("Address", pingManager.AddressField);
            pingManager.PortField = EditorGUILayout.IntField("Port", pingManager.PortField);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ping", EditorStyles.boldLabel);

            if (GUILayout.Button("Send"))  
                pingManager.ButtonSendPing();  
            
                        
            // base.OnInspectorGUI();  what this does? only duplicates everything.

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(pingManager);

            
        }  
    }}

