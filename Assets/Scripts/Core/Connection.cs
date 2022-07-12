using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using MessagePack;
using Mondeto.Core.QuicWrapper;

namespace Mondeto.Core
{

public class Connection : IDisposable
{
    public bool Connected { get; private set; } = false;

    public delegate void OnDisconnectHandler();
    public event OnDisconnectHandler OnDisconnect;

    QuicConnection quicConnection;

    Channel<IDatagramMessage> datagramReceiveChannel = Channel.CreateUnbounded<IDatagramMessage>();

    QuicStream controlStream;
    TaskCompletionSource<QuicStream> controlStreamTCS;
    MessagePackReceiver<IControlMessage> controlReceiver;

    QuicStream blobStream;
    TaskCompletionSource<QuicStream> blobStreamTCS;
    MessagePackReceiver<IBlobMessage> blobReceiver;

    internal Connection(QuicConnection quicConnection)
    {
        this.quicConnection = quicConnection;

        this.quicConnection.DatagramReceived += (_, data) => {
            var msg = MessagePackSerializer.Deserialize<IDatagramMessage>(data);
            datagramReceiveChannel.Writer.WriteAsync(msg);
        };
    }

    public async Task SetupServerAsync()
    {
        Logger.Debug("Connection", "Server setup start");

        // Server waits client to create streams
        // The first created stream is the control stream, and the second is the blob stream
        controlStreamTCS = new TaskCompletionSource<QuicStream>();
        blobStreamTCS = new TaskCompletionSource<QuicStream>();

        quicConnection.PeerStreamStarted += (_, stream) => {
            if (controlStream == null)
            {
                controlStreamTCS.SetResult(stream);
            }
            else if (blobStream == null)
            {
                blobStreamTCS.SetResult(stream);
            }
        };

        controlStream = await controlStreamTCS.Task;
        controlReceiver = new MessagePackReceiver<IControlMessage>(controlStream);

        blobStream = await blobStreamTCS.Task;
        blobReceiver = new MessagePackReceiver<IBlobMessage>(blobStream);

        Connected = true;

        Logger.Debug("Connection", "Server setup complete");
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

        // Client creates streams
        controlStream = quicConnection.OpenStream();
        controlReceiver = new MessagePackReceiver<IControlMessage>(controlStream);

        controlStream.Start(immediate: true);   // immediately notify to server even if no data is transmitted

        blobStream = quicConnection.OpenStream();
        blobReceiver = new MessagePackReceiver<IBlobMessage>(blobStream);

        blobStream.Start(immediate: true);   // immediately notify to server even if no data is transmitted

        Connected = true;

        Logger.Debug("Connection", "Client setup complete");
    }

    public void SendDatagramMessage(IDatagramMessage msg, Action acknowledgeCallback = null)
    {
        var msgBinary = MessagePackSerializer.Serialize(msg);
        if (msgBinary.Length > quicConnection.DatagramMaxLength)
        {
            // Ignore message that is too long.
            // Because acknowledgeCallback is not called, it is same as a packet loss for callers of this function.
            return;
        }
        quicConnection.SendDatagram(msgBinary, acknowledgeCallback);
    }

    public void SendControlMessage(IControlMessage msg)
    {
        controlStream.Send(MessagePackSerializer.Serialize(msg));
    }

    public void SendBlobMessage(IBlobMessage msg)
    {
        blobStream.Send(MessagePackSerializer.Serialize(msg));
    }

    public bool TryReceiveDatagramMessage(out IDatagramMessage msg)
    {
        return datagramReceiveChannel.Reader.TryRead(out msg);
    }

    public bool TryReceiveControlMessage(out IControlMessage msg)
    {
        return controlReceiver.TryReceive(out msg);
    }

    public bool TryReceiveBlobMessage(out IBlobMessage msg)
    {
        return blobReceiver.TryReceive(out msg);
    }

    public async Task<IDatagramMessage> ReceiveDatagramMessageAsync(CancellationToken cancel = default)
    {
        return await datagramReceiveChannel.Reader.ReadAsync(cancel);
    }

    public async Task<IControlMessage> ReceiveControlMessageAsync(CancellationToken cancel = default)
    {
        return await controlReceiver.ReceiveAsync(cancel);
    }

    public async Task<IBlobMessage> ReceiveBlobMessageAsync(CancellationToken cancel = default)
    {
        return await blobReceiver.ReceiveAsync(cancel);
    }

    public void Dispose()
    {
        if (controlReceiver != null)
        {
            controlReceiver.Dispose();
        }

        if (blobReceiver != null)
        {
            blobReceiver.Dispose();
        }

        if (controlStream != null)
        {
            controlStream.Dispose();
        }

        if (blobStream != null)
        {
            blobStream.Dispose();
        }

        quicConnection.Dispose();
    }

    public string GetKeyLog() => quicConnection.GetKeyLog();
}

} // end namespace