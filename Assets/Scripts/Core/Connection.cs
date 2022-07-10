using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Buffers;
using MessagePack;
using System.Threading.Channels;
using Mondeto.Core.QuicWrapper;

namespace Mondeto.Core
{

public class Connection : IDisposable
{
    public enum ChannelType
    {
        Sync = 0, Control = 1, Blob = 2, Audio = 3
    }

    public bool Connected { get; private set; } = false;

    public delegate void OnDisconnectHandler();
    public event OnDisconnectHandler OnDisconnect;

    QuicConnection quicConnection;

    QuicStream controlStream;
    MemoryStream controlMemoryStream = new MemoryStream();
    MessagePackStreamReader controlReader;
    TaskCompletionSource<int> controlStreamDataWaitTCS = new TaskCompletionSource<int>();

    TaskCompletionSource<QuicStream> controlStreamTCS;

    // System.Threading.Channels.Channel for inter-thread communications
    //  (For usage, see https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/ )
    // This is not a built-in class, but (in Unity) can be installed by adding three DLLs.
    //  (see https://yotiky.hatenablog.com/entry/unity_channels )
    Channel<byte[]>[] threadChannels;

    internal Connection(QuicConnection quicConnection)
    {
        this.quicConnection = quicConnection;
    }

    public async Task SetupServerAsync()
    {
        Logger.Debug("Connection", "Server setup start");

        // Server waits client to create the control stream
        controlStreamTCS = new TaskCompletionSource<QuicStream>();
        quicConnection.PeerStreamStarted += OnClientStartedControlStream;

        controlStream = await controlStreamTCS.Task;

        Logger.Debug("Connection", "Server setup complete");
    }

    void OnClientStartedControlStream(QuicConnection _, QuicStream stream)
    {
        // This handler is used only once
        quicConnection.PeerStreamStarted -= OnClientStartedControlStream;

        controlStreamTCS.SetResult(stream);
    }

    public async Task SetupClientAsync(CancellationToken cancel)
    {
        Logger.Debug("Connection", "Client setup start");

        // Wait until connection completes
        var connectTCS = new TaskCompletionSource<int>();
        cancel.Register(() => connectTCS.SetCanceled());
        quicConnection.Connected += (_) => {
            connectTCS.SetResult(0);
        };
        await connectTCS.Task;

        // Client creates the control stream
        controlStream = quicConnection.OpenStream();
        controlStream.Received += OnControlStreamReceived;

        controlReader = new MessagePackStreamReader(controlMemoryStream);

        controlStream.Start(immediate: true);   // immediately notify to server even if no data is transmitted

        Logger.Debug("Connection", "Client setup complete");
    }

    void OnControlStreamReceived(QuicStream stream, byte[] data)
    {
        // TODO: Fix infinite expansion of MemoryStream by regularly clearing.
        controlMemoryStream.Write(data, 0, data.Length);

        controlStreamDataWaitTCS.SetResult(data.Length);
    }

    public void SendControlMessage(IControlMessage msg)
    {
        controlStream.Send(MessagePackSerializer.Serialize(msg));
    }

    // Generic type specification is necessary to specify msg is interface type, not message type itself
    public void SendMessage<T>(ChannelType type, T msg)
    {
        byte[] buf = MessagePackSerializer.Serialize<T>(msg);
        // channels[(int)type].SendMessage(buf);
    }

    public bool TryReceiveMessage<T>(ChannelType type, out T msg)
    {
        byte[] buf;
        if (threadChannels[(int)type].Reader.TryRead(out buf))
        {
            msg = MessagePackSerializer.Deserialize<T>(buf);
            return true;
        }
        else
        {
            msg = default(T);
            return false;
        }
    }

    public async Task<T> ReceiveMessageAsync<T>(ChannelType type, CancellationToken cancel = default)
    {
        byte[] buf = await threadChannels[(int)type].Reader.ReadAsync(cancel);
        return MessagePackSerializer.Deserialize<T>(buf);
    }

    public async Task<IControlMessage> ReceiveControlMessageAsync(CancellationToken cancel = default)
    {
        cancel.Register(() => controlStreamDataWaitTCS.SetCanceled());

        while (true)
        {
            cancel.ThrowIfCancellationRequested();

            // MessagePackStreamReader detects boundary of MessagePack-encoded messages
            // See: https://github.com/neuecc/MessagePack-CSharp#multiple-messagepack-structures-on-a-single-stream
            if (await controlReader.ReadAsync(cancel) is ReadOnlySequence<byte> msgpack)
            {
                return MessagePackSerializer.Deserialize<IControlMessage>(msgpack);
            }
            else
            {
                await controlStreamDataWaitTCS.Task;
                controlStreamDataWaitTCS = new TaskCompletionSource<int>();
            }
        }
    }

    public void Dispose()
    {
        if (controlStream != null)
        {
            controlStream.Dispose();
        }

        quicConnection.Dispose();
    }
}

} // end namespace