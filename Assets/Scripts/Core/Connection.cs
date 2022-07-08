using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MessagePack;
using System.Threading.Channels;

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

    // PeerConnection pc;

    // DataChannel[] channels = new DataChannel[4];
    ChannelType[] channelTypes = { ChannelType.Sync, ChannelType.Control, ChannelType.Blob, ChannelType.Audio };
    string[] channelLabels = { "sync", "control", "blob", "audio" };

    // System.Threading.Channels.Channel for inter-thread communications
    //  (For usage, see https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/ )
    // This is not a built-in class, but (in Unity) can be installed by adding three DLLs.
    //  (see https://yotiky.hatenablog.com/entry/unity_channels )
    Channel<byte[]>[] threadChannels;

    public Connection()
    {
        /*
        pc = new PeerConnection();

        threadChannels = new Channel<byte[]>[channels.Length];
        for (int i = 0; i < channels.Length; i++)
        {
            threadChannels[i] = Channel.CreateUnbounded<byte[]>();
        }
        */
    }

    public async Task SetupAsync(Signaler signaler, bool isServer, uint clientNodeId = 0)
    {
        /*
        await pc.InitializeAsync(new PeerConnectionConfiguration {
            IceServers = new List<IceServer> {
                new IceServer { Urls = { signaler.IceServerUrl } }
            }
        });

        var tcs = new TaskCompletionSource<bool>();

        // Do signaling
        //  https://microsoft.github.io/MixedReality-WebRTC/manual/cs/cs-signaling.html
        //  https://microsoft.github.io/MixedReality-WebRTC/manual/cs/helloworld-cs-signaling-core3.html

        pc.LocalSdpReadytoSend += (SdpMessage sdpMessage) => {
            // ここはawaitではなくWaitにしないとSocketが切れる．スレッドセーフ関係?
            signaler.SendSdpAsync(sdpMessage.Type == SdpMessageType.Offer, sdpMessage.Content, clientNodeId).Wait();
        };
        pc.IceCandidateReadytoSend += (IceCandidate candidate) => {
            signaler.SendIceAsync(candidate.SdpMid, candidate.SdpMlineIndex, candidate.Content, clientNodeId).Wait();
        };

        pc.IceStateChanged += (IceConnectionState state) => {
            Logger.Debug("Connection", $"ICE state changed to {state}");
            // https://microsoft.github.io/MixedReality-WebRTC/versions/release/2.0/api/Microsoft.MixedReality.WebRTC.IceConnectionState.html
            if (state == IceConnectionState.Connected)
            {
                Connected = true;
            }
            if (state == IceConnectionState.Closed || state == IceConnectionState.Disconnected || state == IceConnectionState.Failed)
            {
                Connected = false;
                OnDisconnect();
            }

            if (!isServer && state == IceConnectionState.Failed)
            {
                tcs.SetException(new ConnectionException("Failed to establish a WebRTC connection"));
            }
        };

        signaler.SdpReceived += async (bool isOffer, string sdp, uint cid) => {
            if (isServer && cid != clientNodeId)
            {
                // ignore messages for other clients
                return;
            }

            await pc.SetRemoteDescriptionAsync(new SdpMessage {
                Type = isOffer ? SdpMessageType.Offer : SdpMessageType.Answer,
                Content = sdp
            });
            if (isOffer)
            {
                pc.CreateAnswer();
            }
        };
        signaler.IceReceived += (string sdpMid, int sdpMLineIndex, string candidate, uint cid) => {
            if (isServer && cid != clientNodeId)
            {
                // ignore messages for other clients
                return;
            }

            pc.AddIceCandidate(new IceCandidate {
                SdpMid = sdpMid,
                SdpMlineIndex = sdpMLineIndex,
                Content = candidate
            });
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

            Logger.Debug("Connection", "Server is ready for signaling");
            await signaler.NotifyReadyAsync(clientNodeId);

            Logger.Debug("Connection", "Server: Waiting for DC");
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

            Logger.Debug("Connection", "Client: Waiting for server ready");
            await signaler.WaitReadyAsync();

            pc.CreateOffer();
        }

        foreach (var (dc, idx) in channels.Select((dc, idx) => (dc, idx)))
        {
            dc.MessageReceived += (data) => {
                threadChannels[idx].Writer.TryWrite(data);  // Always succeeds because the Channel is unbounded
            };
            dc.StateChanged += () => {
                Logger.Debug("Connection", $"DC {(ChannelType)idx} state changed to {dc.State}");
                if (dc.State == DataChannel.ChannelState.Closing) {
                    // Disconnect handling
                    Connected = false;
                }
            };
        }

        //await Task.Delay(5000);

        if (!isServer)
        {
            // FIXME: Waiting pc.Connected not work in server (cannot establish a connection to client)
            //        In server, should wait until all DataChannels are added?
            pc.Connected += () => {
                tcs.SetResult(true);
            };
            await tcs.Task;
        }
        */
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

    public void Dispose()
    {
        /*
        pc.Close();
        pc.Dispose();
        */
    }
}

} // end namespace