using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using LitJson;

public class Signaler : IDisposable
{
    public delegate void ClientConnectedHandler(uint clientNodeID);
    public delegate void SdpHandler(bool isOffer, string sdp, uint clientNodeId);
    public delegate void IceHandler(string sdpMid, int sdpMLineIndex, string candidate, uint clientNodeId);

    public event ClientConnectedHandler ClientConnected;
    public event SdpHandler SdpReceived;
    public event IceHandler IceReceived;

    public uint LocalNodeId { get; private set; }

    public string IceServerUrl { get; private set; }

    ClientWebSocket ws;
    string uri;
    bool isServer;

    Task processTask;

    TaskCompletionSource<uint> helloTcs = new TaskCompletionSource<uint>();
    TaskCompletionSource<bool> readyTcs = new TaskCompletionSource<bool>();

    public Signaler(string uri, bool isServer)
    {
        ws = new ClientWebSocket();
        this.uri = uri;
        this.isServer = isServer;
    }

    public async Task<uint> ConnectAsync()
    {
        try
        {
            await ws.ConnectAsync(new Uri(uri), CancellationToken.None);
        }
        catch (WebSocketException e)
        {
            throw new SignalingException("Cannot connect to the signaling server.", e);
        }

        processTask = Task.Run(async () => {
            try
            {
                await ProcessAsync();
            }
            catch (Exception e)
            {
                var wrapped = (e is SignalingException) ? e : new SignalingException("Exception occured during signaling.", e);

                if (!helloTcs.Task.IsCompleted)
                {
                    helloTcs.SetException(wrapped);
                }
                else if (!readyTcs.Task.IsCompleted)
                {
                    readyTcs.SetException(wrapped);
                }
                else
                {
                    Logger.Error("Signaler", (isServer ? "Server: " : "Client: ") + e.ToString());
                }
            }
        });

        return await helloTcs.Task;
    }

    public async Task ProcessAsync()
    {
        var buf = new byte[8192];
        while (ws.State == WebSocketState.Open)
        {
            var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
            JsonData msg;
            try
            {
                msg = JsonMapper.ToObject(Encoding.UTF8.GetString(buf, 0, res.Count));
            }
            catch (JsonException)
            {
                Logger.Error("Signaler", "Invalid JSON");
                Logger.Error("Signaler", Encoding.UTF8.GetString(buf, 0, res.Count));
                continue;
            }

            var type = (string)msg["type"];
            // node ID of the message sender
            var nodeId = isServer ? (uint)(int)msg["nodeID"] : 0;

            if ((string)msg["type"] == "hello")
            {
                LocalNodeId = (uint)(int)msg["nodeID"];

                IceServerUrl = (string)msg["iceServerUrl"];
                Logger.Debug("Signaler", $"Using ICE server {IceServerUrl}");

                if (isServer && LocalNodeId != 0)
                {
                    throw new SignalingException("Cannot register as the server");
                }
                else if (!isServer && LocalNodeId == 0)
                {
                    throw new SignalingException("Cannot register as a client");
                }
                helloTcs.SetResult(LocalNodeId);
            }
            else if (type == "ready")
            {
                readyTcs.SetResult(true);
            }
            else if (type == "clientConnected" && isServer)
            {
                ClientConnected?.Invoke(nodeId);
            }
            else if (type == "sdpOffer" || type == "sdpAnswer")
            {
                SdpReceived?.Invoke(type == "sdpOffer", (string)msg["sdp"], nodeId);
            }
            else if (type == "ice")
            {
                IceReceived?.Invoke((string)msg["sdpMid"], (int)msg["sdpMLineIndex"], (string)msg["candidate"], nodeId);
            }
            else if (type == "error")
            {
                throw new SignalingException("Error message received from the signaling server.");  // FIXME: add message field in error message. change in signaling server is needed
            }
            else
            {
                throw new SignalingException("Invalid message received from the signaling server.");
            }
        }
    }

    public Task NotifyReadyAsync(uint nodeId)
    {
        var msg = new Dictionary<string, object> {
            { "type", "ready" },
            { "nodeID", nodeId }
        };
        // Third arg (endOfMessage) must be true. Otherwise nothing will be sent.
        return ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonMapper.ToJson(msg))),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    public Task WaitReadyAsync()
    {
        return readyTcs.Task;
    }

    public Task SendSdpAsync(bool isOffer, string sdp, uint clientNodeId = 0)
    {
        var msg = new Dictionary<string, object> {
            { "type", isOffer ? "sdpOffer" : "sdpAnswer" },
            { "sdp", sdp }
        };
        if (isServer) msg["nodeID"] = clientNodeId;

        // Third arg (endOfMessage) must be true. Otherwise nothing will be sent.
        return ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonMapper.ToJson(msg))),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public Task SendIceAsync(string sdpMid, int sdpMLineIndex, string candidate, uint clientNodeId = 0)
    {
        var msg = new Dictionary<string, object> {
            { "type", "ice" },
            { "candidate", candidate },
            { "sdpMLineIndex", sdpMLineIndex },
            { "sdpMid", sdpMid }
        };
        if (isServer) msg["nodeID"] = clientNodeId;

        // Third arg (endOfMessage) must be true. Otherwise nothing will be sent.
        return ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonMapper.ToJson(msg))),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void Dispose()
    {
        ws.Dispose();
    }
}