using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Misc;
using TMPro;
using UMUI;
using UnityEngine;

public class StartTab : UiTab
{
    public TextMeshProUGUI lobbyName;
    public TMP_InputField playerName;

    public void Continue()
    {
        // todo comes here, but then "Only" requestsLobbyInformation.
        LobbyManager.instance.RequestLobbyInformation(JoinOrHost);
    }


    public override void UpdateTab()
    {
        base.UpdateTab();
        lobbyName.text = NetworkManager.instance.lobbyName;
    }

    public void JoinOrHost(LobbyInformation[] lobbyInformation)
    {
        GameManager.instance.SetPlayerName( playerName.text==""?GenerateRandomUsername():playerName.text);

        Debug.Log("Join or host");
        
        foreach (var li in lobbyInformation)
        {
            if (li.lobbyName==NetworkManager.instance.lobbyName)
            {
                NetworkManager.instance.JoinSession();
                return;
            }
        }
        NetworkManager.instance.CreateSession();
    }
    
    void Start()
    {
        string randomUsername = GenerateRandomUsername();
        Debug.Log(randomUsername);
    }

    private string GenerateRandomUsername()
    {
        string adjective = adjectives[random.Next(adjectives.Count)];
        string noun = nouns[random.Next(nouns.Count)];
        string personName = personNames[random.Next(personNames.Count)];

        return $"{personName} The {adjective} {noun} ";
    }

 
    private List<string> adjectives = new List<string>
    {
        "Mighty", "Enchanted", "Mystical", "Wise", "Ethereal", "Arcane", "Divine", "Ancient", "Invisible", "Elemental"
    };

    private List<string> nouns = new List<string>
    {
        "Wizard", "Sorcerer", "Mage", "Warlock", "Alchemist", "Enchanter", "Magician", "Spellbinder", "Battlemage", "Summoner"
    };

    private List<string> personNames = new List<string>
    {
        "Zahar", "Thalador", "Eldris", "Alaric", "Kael", "Maelor", "Galdor", "Caelum", "Baelgor", "Ithorin"
    };

    private System.Random random = new System.Random();

}
