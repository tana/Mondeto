using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Threading.Channels;
using System.IO.Pipelines;
using MessagePack;
using Mondeto.Core.QuicWrapper;

namespace Mondeto.Core
{

class MessagePackReceiver<T> : IDisposable
{
    QuicStream stream;

    Pipe pipe = new();

    MessagePackStreamReader mpReader;

    Channel<T> channel = Channel.CreateUnbounded<T>();

    CancellationTokenSource stopTokenSource = new CancellationTokenSource();

    public MessagePackReceiver(QuicStream stream)
    {
        this.stream = stream;

        mpReader = new MessagePackStreamReader(pipe.Reader.AsStream());

        this.stream.Received += OnReceived;

        var stopToken = stopTokenSource.Token;
        // Read messages in another thread
        Task.Run(async () => {
            // MessagePackStreamReader detects boundary of MessagePack-encoded messages
            // See: https://github.com/neuecc/MessagePack-CSharp#multiple-messagepack-structures-on-a-single-stream
            while (!stopToken.IsCancellationRequested)
            {
                if (await mpReader.ReadAsync(stopToken) is ReadOnlySequence<byte> msgpack)
                {
                    var msg = MessagePackSerializer.Deserialize<T>(msgpack);
                    await channel.Writer.WriteAsync(msg, stopToken);
                }
            }
        });
    }

    void OnReceived(QuicStream stream, byte[] data)
    {
        // TODO: Fix infinite expansion of the pipe by regularly clearing.
        Memory<byte> memory = pipe.Writer.GetMemory(data.Length);
        new Span<byte>(data).CopyTo(memory.Span);
        pipe.Writer.Advance(data.Length);

        pipe.Writer.FlushAsync(); // Ignore whether the writer should pause or not
    }

    public async Task<T> ReceiveAsync(CancellationToken cancel = default)
    {
        return await channel.Reader.ReadAsync(cancel);
    }

    public bool TryReceive(out T msg)
    {
        return channel.Reader.TryRead(out msg);
    }

    public void Dispose()
    {
        stopTokenSource.Cancel();

        stream.Received -= OnReceived;
    }
}

}   // end namespace