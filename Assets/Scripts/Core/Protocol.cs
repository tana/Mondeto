using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;

[MessagePack.Union(0, typeof(Primitive<int>))]
[MessagePack.Union(1, typeof(Primitive<uint>))]
[MessagePack.Union(2, typeof(Primitive<long>))]
[MessagePack.Union(3, typeof(Primitive<ulong>))]
[MessagePack.Union(4, typeof(Primitive<float>))]
[MessagePack.Union(5, typeof(Primitive<double>))]
[MessagePack.Union(6, typeof(Primitive<string>))]
[MessagePack.Union(7, typeof(Vec))]
[MessagePack.Union(8, typeof(Quat))]
[MessagePack.Union(9, typeof(BlobHandle))]
public interface IValue
{
}

// TODO inefficient (wrapping values in Primitive<T> requires additional space)
[MessagePackObject]
public class Primitive<T> : IValue
{
    [Key(0)]
    public T Value;
}

// TODO array of IValue

// 3-dimensional vector
[MessagePackObject]
public class Vec : IValue
{
    [Key(0)]
    public float X = 0.0f;
    [Key(1)]
    public float Y = 0.0f;
    [Key(2)]
    public float Z = 0.0f;
}

// Quaternion
[MessagePackObject]
public class Quat : IValue
{
    [Key(0)]
    public float W = 1.0f;
    [Key(1)]
    public float X = 0.0f;
    [Key(2)]
    public float Y = 0.0f;
    [Key(3)]
    public float Z = 0.0f;
}

[MessagePackObject]
public class BlobHandle : IValue
{
    [Key(0)]
    public byte[] Guid;

    public override bool Equals(object obj)
    {
        if (obj is BlobHandle other)
            return Guid.SequenceEqual(other.Guid);
        else
            return false;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        for (int i = 0; i < 4; i++)
        {
            hash ^= (Guid[i] << 24 | Guid[i + 1] << 16 | Guid[i + 2] << 8 | Guid[i + 3]);
        }
        return hash;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in Guid)
        {
            sb.Append($"{b:x}");
        }
        return sb.ToString();
    }
}

// State of an object
/*
[MessagePackObject]
public class ObjectState
{
    [Key(0)]
    public int ObjectTag;
    [Key(1)]
    public Vec Position = new Vec();
    [Key(2)]
    public Quat Rotation = new Quat();
    [Key(3)]
    public Vec Velocity = new Vec();
    [Key(4)]
    public Vec AngularVelocity = new Vec();
}
*/

[MessagePackObject]
public class ObjectUpdate
{
    [Key(0)]
    public uint ObjectId;
    [Key(1)]
    public List<FieldUpdate> Fields;
}

[MessagePackObject]
public class FieldUpdate
{
    [Key(0)]
    public string Name; // TODO
    [Key(1)]
    public IValue Value;
}

// Messages sent through reliable communication channel (TCP)
[MessagePack.Union(0, typeof(HelloMessage))]
[MessagePack.Union(1, typeof(NodeIdMessage))]
[MessagePack.Union(2, typeof(CreateObjectMessage))]
[MessagePack.Union(3, typeof(ObjectCreatedMessage))]
[MessagePack.Union(4, typeof(DeleteObjectMessage))]
[MessagePack.Union(5, typeof(ObjectDeletedMessage))]
[MessagePack.Union(6, typeof(RegisterSymbolMessage))]
[MessagePack.Union(7, typeof(SymbolRegisteredMessage))]
public interface ITcpMessage
{
}

// (Client ot Server)
[MessagePackObject]
public class HelloMessage : ITcpMessage
{
    [Key(0)]
    public int UdpPort;
}

// (Server to Client) Tell the Node ID of a client
[MessagePackObject]
public class NodeIdMessage : ITcpMessage
{
    [Key(0)]
    public uint NodeId;
}

// (Client to Server) Request creation of a new object
[MessagePackObject]
public class CreateObjectMessage : ITcpMessage
{
}

// (Server to Client) Notify that an object is created
[MessagePackObject]
public class ObjectCreatedMessage : ITcpMessage
{
    [Key(0)]
    public uint ObjectId;
    [Key(1)]
    public uint OriginalNodeId;
}

// (Client to Server) Request deletion an object
[MessagePackObject]
public class DeleteObjectMessage : ITcpMessage
{
    [Key(0)]
    public uint ObjectId;
}

// (Server to Client) Notify that an object is deleted
[MessagePackObject]
public class ObjectDeletedMessage : ITcpMessage
{
    [Key(0)]
    public uint ObjectId;
}

// (Client to Server) Request registration of new symbol
[MessagePackObject]
public class RegisterSymbolMessage : ITcpMessage
{
    [Key(0)]
    public string Symbol;
}

// (Server to Client) 
[MessagePackObject]
public class SymbolRegisteredMessage : ITcpMessage
{
    [Key(0)]
    public string Symbol;
    [Key(1)]
    public uint SymbolId;
}

[MessagePack.Union(0, typeof(BlobBodyMessage))]
[MessagePack.Union(1, typeof(BlobInfoMessage))]
[MessagePack.Union(2, typeof(BlobRequestMessage))]
public interface IBlobMessage
{
}

[MessagePackObject]
public class BlobBodyMessage : IBlobMessage
{
    [Key(0)]
    public BlobHandle Handle;
    [Key(1)]
    public uint Offset;
    [Key(2)]
    public byte[] Data;
}

[MessagePackObject]
public class BlobInfoMessage : IBlobMessage
{
    [Key(0)]
    public BlobHandle Handle;
    [Key(1)]
    public uint Size;
    [Key(2)]
    public string MimeType;
}

[MessagePackObject]
public class BlobRequestMessage : IBlobMessage
{
    [Key(0)]
    public BlobHandle Handle;
}

[MessagePackObject]
public class AudioDataMessage
{
    [Key(0)]
    public uint ObjectId;
    [Key(1)]
    public byte[] Data;
}

// Utility functions
public class ProtocolUtil
{
    public static async Task<short> ReadShortAsync(Stream s)
    {
        byte[] buf = new byte[2];
        await s.ReadAsync(buf, 0, 2);
        return (short)((buf[0] << 8) | (buf[1]));
    }

    public static async Task WriteShortAsync(Stream s, short value)
    {
        byte[] buf = new byte[] { (byte)((value & 0xFF00) >> 8), (byte)(value & 0x00FF) };
        await s.WriteAsync(buf, 0, 2);
    }

    public static void WriteShort(Stream s, short value)
    {
        byte[] buf = new byte[] { (byte)((value & 0xFF00) >> 8), (byte)(value & 0x00FF) };
        s.Write(buf, 0, 2);
    }

    public static async Task<ITcpMessage> ReadTcpMessageAsync(Stream stream)
    {
        // MessagePackSerializer.DeserializeAsync is not available in Unity.
        // (see https://github.com/neuecc/MessagePack-CSharp/issues/362 )
        // Therefore, we use ReadAsync and Deserialize(byte[]) instead.
        short msgSize = await ProtocolUtil.ReadShortAsync(stream);
        byte[] buf = new byte[msgSize];
        await stream.ReadAsync(buf, 0, msgSize);
        return MessagePackSerializer.Deserialize<ITcpMessage>(buf);
    }

    public static async Task WriteTcpMessageAsync(Stream stream, ITcpMessage msg)
    {
        byte[] buf = MessagePackSerializer.Serialize(msg);
        await ProtocolUtil.WriteShortAsync(stream, (short)buf.Length);
        await stream.WriteAsync(buf, 0, buf.Length);
    }

    public static void WriteTcpMessage(Stream stream, ITcpMessage msg)
    {
        byte[] buf = MessagePackSerializer.Serialize(msg);
        ProtocolUtil.WriteShort(stream, (short)buf.Length);
        stream.Write(buf, 0, buf.Length);
    }

    public static Task<T> Timeout<T>(int milliseconds, string msg)
    {
        Task.Delay(milliseconds);
        throw new TimeoutException(msg);
    }
}

public class ProtocolException : Exception
{
    public ProtocolException() : base() {}
    public ProtocolException(string msg) : base(msg) {}
}