namespace SynupsNetworking.core.Enums
{
    public enum PacketType
    {
        Connect,
        Disconnect,
        ClientList,
        ActorList,
        Actor,
        Ping,
        PingReply,
        RPC,
        Ack,
        HolePunch,
        PublicEndpoint,
        SetRendezvousInLobby,
        SyncVar,
        DestroyActor,
        TransferOwnerShip,
        CheckOwnership,
        CheckOwnershipReply,
        ActorRequest,
        LobbyInformationRequest,
        LobbyInformationSet,
        Echo,
        EchoReply,
    }


    public enum SerializableType
    {
        Null,
        Int,
        Float,
        String,
        Vector2,
        Vector3,
        Quaternion,
        Bool,
        NetworkIdentity,
        ChatMessage,
        ObjectArray,
        Dictionary,
    }
    
    public enum PlayerStatus
    {
        EstablishingConnection,
        Connecting,
        Connected,
        Disconnecting,
        Disconnected
    }

    public enum ConnectionStatus
    {
        None,
        Mine,
        Local,
        Private,
        Public_Direct,
        Public_Relay,
    }

    public enum TransportChannel
    {
        Unreliable,
        Reliable,
    }

    public enum OwnershipCheck
    {
        NotChecked,
        IsOwner,
        OwnedByOthers,
        CheckTimedOut,
        NonExistent
    }

}