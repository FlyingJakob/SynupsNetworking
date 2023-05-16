using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using UnityEngine;

public class PlayerInfo : NetworkCallbacks
{
    [SyncVar]
    public string name;


}
