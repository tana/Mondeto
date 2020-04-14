using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.MixedReality.WebRTC;
using MessagePack;

public class Connection : IDisposable
{
    public enum ChannelType
    {
        Sync = 0, Control = 1, Blob = 2, Audio = 3
    }

    public bool Connected { get; private set; } = false;

    public delegate void OnDisconnectHandler();
    public event OnDisconnectHandler OnDisconnect;

    PeerConnection pc;

    DataChannel[] channels = new DataChannel[4];
    ChannelType[] channelTypes = { ChannelType.Sync, ChannelType.Control, ChannelType.Blob, ChannelType.Audio };
    string[] channelLabels = { "sync", "control", "blob", "audio" };

    ConcurrentQueue<byte[]>[] queues;

    public Connection()
    {
        pc = new PeerConnection();

        queues = new ConcurrentQueue<byte[]>[channels.Length];
        for (int i = 0; i < queues.Length; i++)
        {
            queues[i] = new ConcurrentQueue<byte[]>();
        }
    }

    public async Task SetupAsync(Signaler signaler, bool isServer, string clientId = "")
    {
        await pc.InitializeAsync(new PeerConnectionConfiguration {
            IceServers = new List<IceServer> {
                new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
            }
        });

        pc.LocalSdpReadytoSend += (string type, string sdp) => {
            // ここはawaitではなくWaitにしないとSocketが切れる．スレッドセーフ関係?
            signaler.SendSdpAsync(type == "offer", sdp, clientId).Wait();
        };
        pc.IceCandidateReadytoSend += (string candidate, int sdpMLineIndex, string sdpMid) => {
            signaler.SendIceAsync(sdpMid, sdpMLineIndex, candidate, clientId).Wait();
        };

        pc.IceStateChanged += (IceConnectionState state) => {
            Logger.Write($"ICE state changed to {state}");
            if (state == IceConnectionState.Connected)
            {
                Connected = true;
            }
            if (state == IceConnectionState.Closed || state == IceConnectionState.Disconnected || state == IceConnectionState.Failed)
            {
                Connected = false;
                OnDisconnect();
            }
        };

        signaler.SdpReceived += (bool isOffer, string sdp, string cid) => {
            if (isServer && cid != clientId)
            {
                // ignore messages for other clients
                return;
            }

            pc.SetRemoteDescription(isOffer ? "offer" : "answer", sdp);
            if (isOffer)
            {
                pc.CreateAnswer();
            }
        };
        signaler.IceReceived += (string sdpMid, int sdpMLineIndex, string candidate, string cid) => {
            if (isServer && cid != clientId)
            {
                // ignore messages for other clients
                return;
            }

            pc.AddIceCandidate(sdpMid, sdpMLineIndex, candidate);
            //Logger.Write((isServer ? "Server: " : "Client: ") + $"{sdpMid} {sdpMLineIndex} {candidate}");
        };

        if (isServer)
        {
            TaskCompletionSource<DataChannel>[] completionSources = channelTypes.Select(
                _ => new TaskCompletionSource<DataChannel>()
            ).ToArray();

            pc.DataChannelAdded += (dc) => {
                foreach (var type in channelTypes)
                {
                    if (dc.Label == channelLabels[(int)type])
                        completionSources[(int)type].SetResult(dc);
                }
            };

            Logger.Write("Server: Server is ready for signaling");
            await signaler.NotifyReadyAsync(clientId);

            Logger.Write("Server: Waiting for DC");
            foreach (var type in channelTypes)
            {
                channels[(int)type] = await completionSources[(int)type].Task;
            }
        }
        else
        {
            // Define channels
            // Sync channel (unreliable)
            channels[(int)ChannelType.Sync] = await pc.AddDataChannelAsync(
                channelLabels[(int)ChannelType.Sync], ordered: false, reliable: false);
            // Message channel (reliable but order is not guaranteed)
            channels[(int)ChannelType.Control] = await pc.AddDataChannelAsync(
                channelLabels[(int)ChannelType.Control], ordered: false, reliable: true);
            // Blob channel (reliable and ordered)
            channels[(int)ChannelType.Blob] = await pc.AddDataChannelAsync(
                channelLabels[(int)ChannelType.Blob], ordered: true, reliable: true);
            // Audio channel (unreliable)
            channels[(int)ChannelType.Audio] = await pc.AddDataChannelAsync(
                channelLabels[(int)ChannelType.Audio], ordered: false, reliable: false);

            Logger.Write("Client: Waiting for server ready");
            await signaler.WaitReadyAsync();

            pc.CreateOffer();
        }

        foreach (var (dc, idx) in channels.Select((dc, idx) => (dc, idx)))
        {
            dc.MessageReceived += (data) => {
                queues[idx].Enqueue(data);
            };
            dc.StateChanged += () => {
                Logger.Write($"DC {(ChannelType)idx} state changed to {dc.State}");
                if (dc.State == DataChannel.ChannelState.Closing) {
                    // Disconnect handling
                    Connected = false;
                }
            };
        }

        //await Task.Delay(5000);

        //var syncTcs = new TaskCompletionSource<bool>();
        /*
        var tcs = new TaskCompletionSource<bool>();
        pc.Connected += () => {
            tcs.SetResult(true);
        };
        await tcs.Task;
        */
    }

    public void SendMessage<T>(ChannelType type, T msg)
    {
        byte[] buf = MessagePackSerializer.Serialize<T>(msg);
        channels[(int)type].SendMessage(buf);
    }

    public bool TryReceiveMessage<T>(ChannelType type, out T msg)
    {
        byte[] buf;
        if (queues[(int)type].TryDequeue(out buf))
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

    public Task<T> ReceiveMessageAsync<T>(ChannelType type, CancellationToken cancel = default)
    {
        return Task.Run(() => {
            T msg;
            while (!TryReceiveMessage<T>(type, out msg))
            {
                // FIXME
            }
            return msg;
        }, cancel);
    }

    public void Dispose()
    {
        pc.Close();
        pc.Dispose();
    }
}