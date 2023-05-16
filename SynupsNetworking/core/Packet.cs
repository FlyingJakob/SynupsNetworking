using System;
using System.IO;
using SynupsNetworking.core.Enums;

namespace SynupsNetworking.core
{
    public class Packet : ICloneable,IComparable
    {
        public PacketType packetType;
        public TransportChannel channel;
        public int receiverID;
        public int senderID;
        public int sequence;
        public int dataLength;
        public byte[] data;

        public const int unspecifiedID = -1;
        
        
        /// <summary>
        /// Creates a packet with the given parameters.
        /// </summary>
        /// <param name="packetType">The type of the packet.</param>
        /// <param name="receiverID">The ID of the receiver of the packet.</param>
        /// <param name="senderID">The ID of the sender of the packet.</param>
        /// <param name="dataLength">The length of the data in the packet.</param>
        /// <param name="data">The data in the packet.</param>
        public Packet(PacketType packetType, int receiverId, int senderID, int dataLength, byte[] data)
        {
            this.packetType = packetType;
            this.receiverID = receiverId;
            this.senderID = senderID;
            this.dataLength = dataLength;
            this.data = data;
        }
        
        public Packet(PacketType packetType, int dataLength, byte[] data)
        {
            this.packetType = packetType;
            this.receiverID = unspecifiedID;
            this.senderID = unspecifiedID;
            this.dataLength = dataLength;
            this.data = data;
        }

        public object Clone()
        {
            Packet clonedPacket = new Packet(packetType, receiverID, senderID, dataLength, data);
            return clonedPacket;
        }

        

        public Packet(){}
        
        public static Packet DeSerialize(byte[] data)
        {
            byte[] packet = Compressor.Decompress(data);
            
            Packet outPacket = new Packet();
            using (MemoryStream stream = new MemoryStream(packet))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    outPacket.packetType = (PacketType)reader.ReadByte();
                    outPacket.channel = (TransportChannel)reader.ReadByte();
                    outPacket.receiverID = reader.ReadUInt16()-1;
                    outPacket.senderID = reader.ReadUInt16()-1;
                    outPacket.sequence = reader.ReadUInt16();
                    outPacket.dataLength = reader.ReadInt32();
                    outPacket.data = reader.ReadBytes(outPacket.dataLength);
                    return outPacket;
                }
            }
        }
        
        public static byte[] Serialize(Packet packet)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)packet.packetType);
                    writer.Write((byte)packet.channel);
                    writer.Write((ushort)(packet.receiverID+1));
                    writer.Write((ushort)(packet.senderID+1));
                    writer.Write((ushort)packet.sequence);
                    writer.Write(packet.dataLength);
                    writer.Write(packet.data);
                    return Compressor.Compress(stream.ToArray());
                }
            }
        }
        
        public byte[] appendBytes(int bytes)
        {
            byte[] intBytes = this.data;
            byte[] newBytes = new byte[bytes];
            byte[] combinedBytes = new byte[intBytes.Length + newBytes.Length];
            Buffer.BlockCopy(intBytes, 0, combinedBytes, 0, intBytes.Length); /* Adds integer bytes to combinedBytes array. */
            Buffer.BlockCopy(newBytes, 0, combinedBytes, intBytes.Length, newBytes.Length); /* New bytes appended to integer bytes. */
            return combinedBytes;
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            Packet otherPacket = obj as Packet;
            if (otherPacket != null)
                return this.sequence.CompareTo(otherPacket.sequence);
            else
                throw new ArgumentException("Object is not a Packet");
        }
    }
    
    
}