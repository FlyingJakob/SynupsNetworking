using TMPro;
using UnityEngine;

namespace SampleGame.Scripts
{
    public class LobbyItem : MonoBehaviour
    {
        public string lobbyName;
        public TextMeshProUGUI lobbyInfo;
        public MenuTab menuTab;

        public void JoinLobby()
        {
            menuTab.Join(lobbyName);
        }
    }
}