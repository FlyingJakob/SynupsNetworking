using System;
using UnityEngine.Serialization;

namespace SynupsNetworking.core.Misc
{
    [Serializable]
    public class LobbyInformation
    {
        public string lobbyName;
        public int players;
        public int maxPlayers;
        public int delay;

        /* Calculated by LobbyManager when requesting LobbyInformation
         added together with tracker's delay to server, gives (a rough estimate) of 
         the QoS the player can expect for a lobby. */
     
        public LobbyInformation(string lobbyName, int players, int maxPlayers)
        {
            this.lobbyName = lobbyName;
            this.players = players;
            this.maxPlayers = maxPlayers;
            this.delay = 999;
        }
        
        public LobbyInformation(string lobbyName, int players, int maxPlayers, int delay)
        {
            this.lobbyName = lobbyName;
            this.players = players;
            this.maxPlayers = maxPlayers;
            this.delay = delay;
        }
    }

}