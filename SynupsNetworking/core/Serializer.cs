using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using SynupsNetworking.components;
using SynupsNetworking.core.Enums;
using SynupsNetworking.core.Misc;
using UnityEngine;

namespace SynupsNetworking.core
{
    public class Serializer
    {
        #region Actor

        public static byte[] SerializeNetIDOwnership((int, OwnershipCheck) netIdOwnerShip)
        {
         
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(netIdOwnerShip.Item1); 
                    writer.Write((int)netIdOwnerShip.Item2);

                    return stream.ToArray();
                }
            }
        }
        
        public static (int, OwnershipCheck) DeserializeNetIDOwnership(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int item1 = reader.ReadInt32();
                    OwnershipCheck item2 = (OwnershipCheck)reader.ReadInt32(); 

                    return (item1, item2);
                }
            }
        }
        
        
        
        public static byte[] SerializeActor(Actor actor)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(actor.clientID);
                    writer.Write(actor.netID);
                    writer.Write(actor.prefabID);

                    writer.Write(actor.position.x);
                    writer.Write(actor.position.y);
                    writer.Write(actor.position.z);

                    writer.Write(actor.rotation.x);
                    writer.Write(actor.rotation.y);
                    writer.Write(actor.rotation.z);

                    return stream.ToArray();
                }
            }
        }

        public static Actor DeserializeActor(byte[] bytes)
        {
            Actor actor = new Actor();


            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    actor.clientID = reader.ReadInt32();
                    actor.netID = reader.ReadInt32();
                    actor.prefabID = reader.ReadInt32();


                    var px = reader.ReadSingle();
                    var py = reader.ReadSingle();
                    var pz = reader.ReadSingle();

                    actor.position = new Vector3(px, py, pz);

                    var rx = reader.ReadSingle();
                    var ry = reader.ReadSingle();
                    var rz = reader.ReadSingle();

                    actor.rotation = new Vector3(rx, ry, rz);

                    return actor;
                }
            }
        }
        
        
        public static byte[] SerializeDestroyActor(Actor actor)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(actor.clientID);
                    writer.Write(actor.netID);
                    return stream.ToArray();
                }
            }
        }

        public static Actor DeserializeDestroyActor(byte[] bytes)
        {
            Actor actor = new Actor();
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    actor.clientID = reader.ReadInt32();
                    actor.netID = reader.ReadInt32();
                    return actor;
                }
            }
        }

        public static byte[] SerializeActorList(List<Actor> deserializedObj)
        {
            byte[] serializedObj;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    //Size of list
                    writer.Write((int)deserializedObj.Count);

                    foreach (var actor in deserializedObj)
                    {
                        writer.Write(Serializer.SerializeActor(actor));
                    }

                    return stream.ToArray();
                }
            }

            return serializedObj;
        }

        public static List<Actor> DeserializeActorList(byte[] serializedObj)
        {
            List<Actor> outList = new List<Actor>();

            using (MemoryStream stream = new MemoryStream(serializedObj))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int count = reader.ReadInt32();

                    for (int i = 0; i < count; i++)
                    {
                        Actor actor = Serializer.DeserializeActor(reader.ReadBytes(36));
                        outList.Add(actor);
                    }

                    return outList;
                }
            }
        }

        #endregion

        #region IPEndpoint

        public static byte[] SerializeIPEndPoint(IPEndPoint endpoint)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Write the endpoint address bytes
                byte[] addressBytes = endpoint.Address.GetAddressBytes();
                writer.Write(addressBytes);

                // Write the endpoint port
                writer.Write(((ushort)endpoint.Port));

                return stream.ToArray();
            }
        }

        public static IPEndPoint DeserializeIPEndPoint(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // Read the endpoint address bytes
                byte[] addressBytes = reader.ReadBytes(4);

                // Read the endpoint port
                ushort port = reader.ReadUInt16();

                IPAddress address = new IPAddress(addressBytes);
                IPEndPoint endpoint = new IPEndPoint(address, port);
                return endpoint;
            }
        }

        public static byte[] SerializePublicAndPrivateEP(IPEndPoint ipEndpoint, IPEndPoint trackerEndPoint)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(ipEndpoint.Address.ToString());
                writer.Write(ipEndpoint.Port);

                writer.Write(trackerEndPoint.Address.ToString());
                writer.Write(trackerEndPoint.Port);
                return stream.ToArray();
            }
        }

        public static (IPEndPoint, IPEndPoint) DeserializePublicAndPrivateEP(byte[] data)
        {
            string localAddress = "";
            int localPort = 0;

            string trackerAddress = "";
            int trackerPort = 0;


            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // Read the endpoint address bytes
                localAddress = reader.ReadString();
                localPort = reader.ReadInt32();
                trackerAddress = reader.ReadString();
                trackerPort = reader.ReadInt32();

                return (new IPEndPoint(IPAddress.Parse(localAddress), localPort),
                    new IPEndPoint(IPAddress.Parse(trackerAddress), trackerPort));
            }
        }

        public static byte[] SerializeIpEndpointWithString(IPEndPoint endpoint, string lobbyName)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Write the endpoint address bytes
                byte[] addressBytes = endpoint.Address.GetAddressBytes();
                writer.Write(addressBytes);
                writer.Write(endpoint.Port);
                writer.Write(lobbyName);

                return stream.ToArray();
            }
        }

        #endregion

        #region NetworkCallbacks

        public static byte[] SerializeRPC(RPCRequest rpcRequest)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((ushort)(rpcRequest.clientID+1));
                    writer.Write((ushort)rpcRequest.netID);
                    writer.Write((ushort)rpcRequest.componentID);
                    writer.Write((ushort)rpcRequest.methodIndex);
                    byte[] parameters = SerializeObjectParams(rpcRequest.parameters);
                    writer.Write((ushort)parameters.Length);
                    writer.Write(parameters);
                    return stream.ToArray();
                }
            }
        }

        public static RPCRequest DeserializeRPC(byte[] rpc)
        {
            RPCRequest outPPC = new RPCRequest();
            using (MemoryStream stream = new MemoryStream(rpc))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    outPPC.clientID = reader.ReadUInt16()-1;
                    outPPC.netID = reader.ReadUInt16();
                    outPPC.componentID = reader.ReadUInt16();
                    outPPC.methodIndex = reader.ReadUInt16();
                    int parametersLength = reader.ReadUInt16();
                    outPPC.parameters = DeserializeObjectParams(reader.ReadBytes(parametersLength));

                    return outPPC;
                }
            }
        }

        public static byte[] SerializeSyncVar(SyncVarRequest syncVar)
        {
            
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(syncVar.clientID);
                    writer.Write(syncVar.netID);
                    writer.Write(syncVar.componentID);
                    
                    //byte[] values = SerializeObjectParams(syncVar.values);
                    byte[] values = SerializeDictionary((Dictionary<object,object>)syncVar.values);
                    writer.Write(values.Length);
                    writer.Write(values);
                    return stream.ToArray();
                }
            }
        }

        public static SyncVarRequest DeserializeSyncVar(byte[] syncVar)
        {
            SyncVarRequest outPPC = new SyncVarRequest();
            using (MemoryStream stream = new MemoryStream(syncVar))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    outPPC.clientID = reader.ReadInt32();
                    outPPC.netID = reader.ReadInt32();
                    outPPC.componentID = reader.ReadInt32();
                    int values = reader.ReadInt32();
                    outPPC.values = DeserializeDictionary(reader.ReadBytes(values));

                    return outPPC;
                }
            }
        }

        public static byte[] SerializeObjectParams(object[] parameters)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((ushort)parameters.Length);
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        object obj = parameters[i];

                        if (obj is null)
                        {
                            writer.Write((byte)SerializableType.Null);
                            break;
                        }
                        
                        switch (Type.GetTypeCode(obj.GetType()))
                        {
                            case TypeCode.Int32:
                                writer.Write((byte)SerializableType.Int);
                                writer.Write((int)obj);
                                break;
                            case TypeCode.Single:
                                writer.Write((byte)SerializableType.Float);
                                writer.Write((float)obj);
                                break;
                            case TypeCode.Boolean:
                                writer.Write((byte)SerializableType.Bool);
                                writer.Write((bool)obj);
                                break;
                            case TypeCode.String:
                                writer.Write((byte)SerializableType.String);
                                writer.Write((string)obj);
                                break;
                            case TypeCode.Object:
                                
                                switch (obj)
                                {
                                        
                                    case Vector3 v3:
                                        writer.Write((byte)SerializableType.Vector3);
                                        writer.Write(v3.x);
                                        writer.Write(v3.y);
                                        writer.Write(v3.z);
                                        break;
                                    case Vector2 v2:
                                        writer.Write((byte)SerializableType.Vector2);
                                        writer.Write(v2.x);
                                        writer.Write(v2.y);
                                        break;
                                    case Quaternion q:
                                        writer.Write((byte)SerializableType.Quaternion);
                                        writer.Write(q.x);
                                        writer.Write(q.y);
                                        writer.Write(q.z);
                                        writer.Write(q.w);
                                        break;
                                    case NetworkIdentity networkIdentity:
                                        writer.Write((byte)SerializableType.NetworkIdentity);
                                        writer.Write(networkIdentity.ownerID);
                                        writer.Write(networkIdentity.netID);
                                        break;
                                    case ChatMessage chatMessage:
                                        writer.Write((byte)SerializableType.ChatMessage);
                                        writer.Write(chatMessage.sender);
                                        writer.Write(chatMessage.message);
                                        break;
                                    case object[] objectArray :
                                        writer.Write((byte)SerializableType.ObjectArray);
                                        byte[] nestedBytes = SerializeObjectParams(objectArray);
                                        writer.Write(nestedBytes.Length);
                                        writer.Write(nestedBytes);
                                        break;
                                    case Dictionary<object,object> dictionary:
                                        writer.Write((byte)SerializableType.Dictionary);
                                        object[] keys = dictionary.Keys.ToArray();
                                        object[] values = dictionary.Keys.ToArray();
                                        byte[] keysBytes = SerializeObjectParams(keys);
                                        byte[] valuesBytes = SerializeObjectParams(values);
                                        
                                        writer.Write(keysBytes.Length);
                                        writer.Write(keysBytes);

                                        writer.Write(valuesBytes.Length);
                                        writer.Write(valuesBytes);
                                        break;
                                }

                                break;
                        }
                    }

                    return stream.ToArray();
                }
            }
        }

        public static object[] DeserializeObjectParams(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    object[] objects = new object[reader.ReadUInt16()];

                    for (int i = 0; i < objects.Length; i++)
                    {
                        SerializableType type = (SerializableType)reader.ReadByte();
                        switch (type)
                        {
                            case SerializableType.Null:
                                objects[i] = null;
                                break;
                            case SerializableType.Int:
                                objects[i] = reader.ReadInt32();
                                break;
                            case SerializableType.Float:
                                objects[i] = reader.ReadSingle();
                                break;
                            case SerializableType.Bool:
                                objects[i] = reader.ReadBoolean();
                                break;
                            case SerializableType.String:
                                objects[i] = reader.ReadString();
                                break;
                            case SerializableType.Vector3:
                                Vector3 v3 = new Vector3();
                                v3.x = reader.ReadSingle();
                                v3.y = reader.ReadSingle();
                                v3.z = reader.ReadSingle();
                                objects[i] = v3;
                                break;
                            case SerializableType.Vector2:
                                Vector2 v2 = new Vector2();
                                v2.x = reader.ReadSingle();
                                v2.y = reader.ReadSingle();
                                objects[i] = v2;
                                break;
                            case SerializableType.Quaternion:
                                Quaternion q = new Quaternion();
                                q.x = reader.ReadSingle();
                                q.y = reader.ReadSingle();
                                q.z = reader.ReadSingle();
                                q.w = reader.ReadSingle();
                                objects[i] = q;
                                break;
                            case SerializableType.NetworkIdentity:
                                objects[i] = NetworkManager.instance.GetNetworkIdentity(reader.ReadInt32(), reader.ReadInt32());
                                break;
                            case SerializableType.ChatMessage:
                                objects[i] = new ChatMessage(reader.ReadString(),reader.ReadString());
                                break;
                            case SerializableType.ObjectArray:
                                int length = reader.ReadInt32();
                                objects[i] = DeserializeObjectParams(reader.ReadBytes(length));
                                break;
                            case SerializableType.Dictionary:

                                int kbL = reader.ReadInt32();
                                byte[] keysBytes = reader.ReadBytes(kbL);
                                
                                int vbL = reader.ReadInt32();
                                byte[] valuesBytes = reader.ReadBytes(vbL);
                                
                                object[] keys = DeserializeObjectParams(keysBytes);
                                object[] values = DeserializeObjectParams(valuesBytes);
                                Dictionary<object, object> output = new Dictionary<object, object>();
                                for (int j = 0; j < keys.Length; j++)
                                {
                                    output.Add(keys[i], values[i]);
                                }
                                objects[i] = output;
                                break;
                            
                            default:
                                Debug.LogError("Type not supported for RPC");
                                return null;
                        }
                    }

                    return objects;
                }
            }
        }

        public static byte[] SerializeDictionary(Dictionary<object, object> dictionary)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    object[] keys = dictionary.Keys.ToArray();
                    object[] values = dictionary.Values.ToArray();
                    byte[] keysBytes = SerializeObjectParams(keys);
                    byte[] valuesBytes = SerializeObjectParams(values);
                                        
                    writer.Write(keysBytes.Length);
                    writer.Write(keysBytes);

                    writer.Write(valuesBytes.Length);
                    writer.Write(valuesBytes);
                    return stream.ToArray();
                }
            }
        }

        public static Dictionary<object, object> DeserializeDictionary(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int kbL = reader.ReadInt32();
                    byte[] keysBytes = reader.ReadBytes(kbL);
                                
                    int vbL = reader.ReadInt32();
                    byte[] valuesBytes = reader.ReadBytes(vbL);
                                
                    object[] keys = DeserializeObjectParams(keysBytes);
                    object[] values = DeserializeObjectParams(valuesBytes);
                    
                    Dictionary<object, object> output = new Dictionary<object, object>();
                    
                    for (int j = 0; j < keys.Length; j++)
                    {
                        output.Add(keys[j], values[j]);
                    }

                    return output;
                }
            }
            
        }


        #endregion

        #region Clients

        public static byte[] SerializeClientDictionary(Dictionary<int, Client> clients)
        {
            byte[] serializedObj;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    //Size of list
                    writer.Write((int)clients.Count);

                    foreach (var kvp in clients)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(SerializeIPEndPoint(kvp.Value.publicEP));
                        writer.Write(SerializeIPEndPoint(kvp.Value.privateEP));

                    }
                    return stream.ToArray();
                }
            }

            return serializedObj;
        }
        public static Dictionary<int, Client> DeserializeClientDictionary(byte[] serializedObj)
        {
            Dictionary<int, Client> clients = new Dictionary<int, Client>();

            using (MemoryStream stream = new MemoryStream(serializedObj))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int count = reader.ReadInt32();


                    for (int i = 0; i < count; i++)
                    {
                        int key = reader.ReadInt32();
                        IPEndPoint publicClient = DeserializeIPEndPoint(reader.ReadBytes(6));
                        IPEndPoint privateClient = DeserializeIPEndPoint(reader.ReadBytes(6));
                        clients.Add(key, new Client(publicClient,privateClient));
                        
                    }

                    return clients;
                }
            }
        }
        
        public static byte[] SerializeConnectPacket(IPEndPoint ipEndpoint, IPEndPoint trackerEndPoint,int clientID)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(ipEndpoint.Address.ToString());
                writer.Write(ipEndpoint.Port);

                writer.Write(trackerEndPoint.Address.ToString());
                writer.Write(trackerEndPoint.Port);
                writer.Write(clientID);
                return stream.ToArray();
            }
        }
        public static (IPEndPoint, IPEndPoint,int) DeserializeConnectPacket(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // Read the endpoint address bytes
                var localAddress = reader.ReadString();
                var localPort = reader.ReadInt32();
                var trackerAddress = reader.ReadString();
                var trackerPort = reader.ReadInt32();
                var clientID = reader.ReadInt32();
                return (new IPEndPoint(IPAddress.Parse(localAddress), localPort), new IPEndPoint(IPAddress.Parse(trackerAddress), trackerPort),clientID);
            }
        }

        #endregion

        #region TURN

        public static byte[] SerializeRelayPacket(byte[] data, IPEndPoint endpoint)
        {
            byte[] endpointBytes = SerializeIPEndPoint(endpoint);
            byte[] sendData = ConcatenateByteArrays(data, endpointBytes);
            return sendData;
        }

        public static void DeserializeRelayPacket(byte[] combinedBytes, out byte[] data, out IPEndPoint endpoint)
        {
            int dataLength = combinedBytes.Length - 6;
            data = new byte[dataLength];

            byte[] epByte = new byte[6];

            Buffer.BlockCopy(combinedBytes, dataLength, epByte, 0, 6);

            endpoint = DeserializeIPEndPoint(epByte);

            // Copy data bytes from combined byte array
            Buffer.BlockCopy(combinedBytes, 0, data, 0, dataLength);
        }

        private static byte[] ConcatenateByteArrays(byte[] array1, byte[] array2)
        {
            byte[] concatenatedArray = new byte[array1.Length + array2.Length];
            Buffer.BlockCopy(array1, 0, concatenatedArray, 0, array1.Length);
            Buffer.BlockCopy(array2, 0, concatenatedArray, array1.Length, array2.Length);
            return concatenatedArray;
        }

        #endregion

        #region Lobby

        public static byte[] SerializeLobbyInformation(LobbyInformation lobbyInformation)
        {
            byte[] serializedObj;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(lobbyInformation.lobbyName);
                    writer.Write(lobbyInformation.players);
                    writer.Write(lobbyInformation.maxPlayers);
                    writer.Write(lobbyInformation.delay); /* Serialized datetime as ticks (long) */

                    serializedObj = stream.ToArray();
              
                    return serializedObj;
                }
            }
        }
        public static LobbyInformation DeserializeLobbyInformation(byte[] serializedObj)
        {
            using (MemoryStream stream = new MemoryStream(serializedObj))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                
                    String lobbyName = reader.ReadString();
                    int players = reader.ReadInt32();
                    int maxPlayers = reader.ReadInt32();
                    int delay = reader.ReadInt32(); // Deserialize the DateTime ticks (long)
               
                    return new LobbyInformation(lobbyName,players,maxPlayers,delay);
                }
            }
        }
        
        public static byte[] SerializeLobbyInformationList(LobbyInformation[] lobbyInformation)
        {
            byte[] serializedObj;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    
                    writer.Write(lobbyInformation.Length);

                    for (int i = 0; i < lobbyInformation.Length; i++)
                    {
                        writer.Write(lobbyInformation[i].lobbyName);
                        writer.Write(lobbyInformation[i].players);
                        writer.Write(lobbyInformation[i].maxPlayers);
                        writer.Write(lobbyInformation[i].delay);
                    }
                    
                    return stream.ToArray();
                }
            }
        }
        public static LobbyInformation[] DeserializeLobbyInformationList(byte[] serializedObj)
        {
            using (MemoryStream stream = new MemoryStream(serializedObj))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int length = reader.ReadInt32();            
                    LobbyInformation[] output = new LobbyInformation[length];

                    for (int i = 0; i < length; i++)
                    {
                        String lobbyName = reader.ReadString();
                        int players = reader.ReadInt32();
                        int maxPlayers = reader.ReadInt32();
                        int delay = reader.ReadInt32();
                        output[i] = new LobbyInformation(lobbyName,players,maxPlayers,delay);
                   
                    }
                    return output;
                }
            }
        }



        #endregion
        #region ReliableProtocol



        public static LatencyDiagnostic DeserializeLatencyDiagnostic(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    long dateTimeTicks = reader.ReadInt64(); // Deserialize the DateTime ticks (long)
                    DateTime time = new DateTime(dateTimeTicks); // Create a new DateTime object from the ticks
            
                    return new LatencyDiagnostic(time);
                }
            }
        }
        public static byte[] SerializeLatencyDiagnostic(LatencyDiagnostic latencyDiagnostic)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(latencyDiagnostic.sentTimeStamp.Ticks); /* Serialized datetime as ticks (long) */
                    
                    return stream.ToArray();
                }
            }
        }
        
        public static byte[] SerializeAckPacket(int packetID)
        {
            return BitConverter.GetBytes(packetID);
        }

        public static int DeserializeAckPacket(byte[] data)
        {
            return BitConverter.ToInt32(data);
        }

        #endregion
    }
}