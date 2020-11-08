using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;

public enum TypeCode
{
    Int = 0,
    Long = 2,
    Float = 4,
    Double = 5,
    String = 6,
    Vec = 7,
    Quat = 8,
    BlobHandle = 9,
    Sequence = 10,
    ObjectRef = 11
}

[MessagePack.Union((int)TypeCode.Int, typeof(Primitive<int>))]
[MessagePack.Union((int)TypeCode.Long, typeof(Primitive<long>))]
[MessagePack.Union((int)TypeCode.Float, typeof(Primitive<float>))]
[MessagePack.Union((int)TypeCode.Double, typeof(Primitive<double>))]
[MessagePack.Union((int)TypeCode.String, typeof(Primitive<string>))]
[MessagePack.Union((int)TypeCode.Vec, typeof(Vec))]
[MessagePack.Union((int)TypeCode.Quat, typeof(Quat))]
[MessagePack.Union((int)TypeCode.BlobHandle, typeof(BlobHandle))]
[MessagePack.Union((int)TypeCode.Sequence, typeof(Sequence))]
[MessagePack.Union((int)TypeCode.ObjectRef, typeof(ObjectRef))]
public interface IValue
{
    TypeCode Type { get; }
}

// TODO inefficient (wrapping values in Primitive<T> requires additional space)
[MessagePackObject]
public class Primitive<T> : IValue
{
    [Key(0)]
    public T Value;

    public Primitive() { }

    public Primitive(T value)
    {
        Value = value;
    }

    [IgnoreMember]
    public TypeCode Type {
        get
        {
            switch (Value)
            {
                case int _:
                    return TypeCode.Int;
                case long _:
                    return TypeCode.Long;
                case float _:
                    return TypeCode.Float;
                case double _:
                    return TypeCode.Double;
                case string _:
                    return TypeCode.String;
                default:
                    return TypeCode.Int;    // TODO: ensure this never happens
            }
        }
    }

    public override bool Equals(object obj)
    {
        return (obj is Primitive<T> other) && (other != null) && (Value.Equals(other.Value));
    }

    public override int GetHashCode() => this.GetHashCode();
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

    public Vec() { }

    public Vec(float x, float y, float z) {
        X = x; Y = y; Z = z;
    }

    [IgnoreMember]
    public TypeCode Type { get => TypeCode.Vec; }

    public static Vec operator+(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec operator-(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec operator*(float k, Vec v)
    {
        return new Vec { X = k * v.X, Y = k * v.Y, Z = k * v.Z };
    }

    public static Vec operator*(Vec v, float k)
    {
        return k * v;
    }

    public float MagnitudeSquare() => X * X + Y * Y + Z * Z;
    public float Magnitude() => (float)Math.Sqrt(MagnitudeSquare());

    public Vec Normalize()
    {
        float mag = Magnitude();
        return new Vec(X / mag, Y / mag, Z / mag);
    }
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

    public Quat() { }
    public Quat(float w, float x, float y, float z)
    {
        W = w; X = x; Y = y; Z = z;
    }

    [IgnoreMember]
    public TypeCode Type { get => TypeCode.Quat; }

    // Quaternion operations
    //   https://mathworld.wolfram.com/Quaternion.html

    public static Quat operator*(Quat a, Quat b)
    {
        return new Quat {
            W = a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            X = a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            Y = a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            Z = a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X
        };
    }
    // rotate a vector
    public static Vec operator*(Quat q, Vec v)
    {
        Quat vQuat = new Quat { W = 0, X = v.X, Y = v.Y, Z = v.Z };
        Quat after = q * vQuat * q.Conjugate();
        return new Vec(after.X, after.Y, after.Z);
    }
    public Quat Conjugate()
    {
        return new Quat { W = W, X = -X, Y = -Y, Z = -Z };
    }

    // angle is in radians
    public static Quat FromAngleAxis(float angle, Vec axis)
    {
        Vec normalized = axis.Normalize();
        double s = Math.Sin(angle / 2.0);
        return new Quat {
            W = (float)Math.Cos(angle / 2.0),
            X = (float)(normalized.X * s),
            Y = (float)(normalized.Y * s),
            Z = (float)(normalized.Z * s)
        };
    }

    // The order of rotation is same as Unity (first z, second x, finally y)
    //  https://docs.unity3d.com/2019.3/Documentation/ScriptReference/Quaternion.Euler.html
    // x,y,z are in radians
    public static Quat FromEuler(float x, float y, float z)
    {
        Quat zRot = Quat.FromAngleAxis(z, new Vec(0, 0, 1));
        Quat xRot = Quat.FromAngleAxis(x, new Vec(1, 0, 0));
        Quat yRot = Quat.FromAngleAxis(y, new Vec(0, 1, 0));
        // TODO: Clarify about rotation order
        // In Unity, q1*q2 means "first rotate using q1, then rotate using q2".
        //  https://docs.unity3d.com/2019.3/Documentation/ScriptReference/Quaternion-operator_multiply.html
        // However, with zRot*xRot*yRot, result does not match with Unity's Quaternion.Euler function.
        // The yRot*xRot*zRot (below) works, however, I am very confused...
        return yRot * xRot * zRot;
    }
}

[MessagePackObject]
public class BlobHandle : IValue
{
    [Key(0)]
    public byte[] Hash;  // SHA-256 hash (32 bytes) of blob data

    [IgnoreMember]
    public TypeCode Type { get => TypeCode.BlobHandle; }

    public override bool Equals(object obj)
    {
        if (obj is BlobHandle other)
            return Hash.SequenceEqual(other.Hash);
        else
            return false;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        for (int i = 0; i < 8; i++)
        {
            hash ^= (Hash[4 * i] << 24 | Hash[4 * i + 1] << 16 | Hash[4 * i + 2] << 8 | Hash[4 * i + 3]);
        }
        return hash;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in Hash)
        {
            sb.Append($"{b:x}");
        }
        return sb.ToString();
    }
}

// TODO reconsider naming
[MessagePackObject]
public class Sequence : IValue
{
    [Key(0)]
    public List<IValue> Elements = new List<IValue>();

    public Sequence() {}
    public Sequence(IList<IValue> elements)
    {
        Elements = elements.ToList();
    }

    [IgnoreMember]
    public TypeCode Type { get => TypeCode.Sequence; }

    public override bool Equals(object obj)
    {
        if (obj is Sequence other)
            return Elements.SequenceEqual(other.Elements);
        else
            return false;
    }

    public override int GetHashCode()
    {
        return Elements.Aggregate(0, (accum, elem) => accum ^ elem.GetHashCode());
    }
}

[MessagePackObject]
public class ObjectRef : IValue
{
    [Key(0)]
    public uint Id;

    [IgnoreMember]
    public TypeCode Type { get => TypeCode.ObjectRef; }
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

// Messages sent through unreliable and unordered Sync Channel
[MessagePack.Union(0, typeof(UpdateMessage))]
[MessagePack.Union(1, typeof(AckMessage))]
public interface ISyncMessage
{
}

[MessagePackObject]
public class UpdateMessage : ISyncMessage
{
    [Key(0)]
    public uint Tick;
    [Key(1)]
    public List<ObjectUpdate> ObjectUpdates;
}

[MessagePackObject]
public class AckMessage : ISyncMessage
{
    [Key(0)]
    public uint AcknowledgedTick;
}

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
[MessagePack.Union(8, typeof(EventSentMessage))]
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

// (Both directions)
[MessagePackObject]
public class EventSentMessage : ITcpMessage
{
    [Key(0)]
    public string Name; // Name of the event
    [Key(1)]
    public uint Sender; // Object ID of the sender
    [Key(2)]
    public uint Receiver;   // Object ID of the receiver
    [Key(3)]
    public IValue[] Args;
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