using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using LitJson;

public class Signaler : IDisposable
{
    public delegate void ClientConnectedHandler(string clientId);
    public delegate void SdpHandler(bool isOffer, string sdp, string clientId);
    public delegate void IceHandler(string sdpMid, int sdpMLineIndex, string candidate, string clientId);

    public event ClientConnectedHandler ClientConnected;
    public event SdpHandler SdpReceived;
    public event IceHandler IceReceived;

    public string LocalClientId { get; private set; }

    ClientWebSocket ws;
    string uri;
    bool isServer;

    Task processTask;

    TaskCompletionSource<string> helloTcs = new TaskCompletionSource<string>();
    TaskCompletionSource<bool> readyTcs = new TaskCompletionSource<bool>();

    public Signaler(string uri, bool isServer)
    {
        ws = new ClientWebSocket();
        this.uri = uri;
        this.isServer = isServer;
    }

    public async Task ConnectAsync()
    {
        await ws.ConnectAsync(new Uri(uri), CancellationToken.None);

        processTask = Task.Run(async () => {
            try
            {
                await ProcessAsync();
            }
            catch (Exception e)
            {
                Logger.Write((isServer ? "Server: " : "Client: ") + e.ToString());
            }
        });

        await helloTcs.Task;
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
                Logger.Write("Invalid JSON");
                Logger.Write(Encoding.UTF8.GetString(buf, 0, res.Count));
                continue;
            }
            var type = (string)msg["type"];
            var clientId = isServer ? (string)msg["clientID"] : "";
            if ((string)msg["type"] == "hello")
            {
                LocalClientId = (string)msg["clientID"];
                if (isServer && LocalClientId != "server")
                {
                    throw new Exception("Cannot register as the server");
                }
                helloTcs.SetResult(LocalClientId);
            }
            else if (type == "ready")
            {
                readyTcs.SetResult(true);
            }
            else if (type == "clientConnected" && isServer)
            {
                ClientConnected?.Invoke(clientId);
            }
            else if (type == "sdpOffer" || type == "sdpAnswer")
            {
                SdpReceived?.Invoke(type == "sdpOffer", (string)msg["sdp"], clientId);
            }
            else if (type == "ice")
            {
                IceReceived?.Invoke((string)msg["sdpMid"], (int)msg["sdpMLineIndex"], (string)msg["candidate"], clientId);
            }
        }
    }

    public Task NotifyReadyAsync(string clientId)
    {
        var msg = new Dictionary<string, object> {
            { "type", "ready" },
            { "clientID", clientId }
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

    public Task SendSdpAsync(bool isOffer, string sdp, string clientId = "")
    {
        var msg = new Dictionary<string, object> {
            { "type", isOffer ? "sdpOffer" : "sdpAnswer" },
            { "sdp", sdp }
        };
        if (isServer) msg["clientID"] = clientId;

        // Third arg (endOfMessage) must be true. Otherwise nothing will be sent.
        return ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonMapper.ToJson(msg))),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public Task SendIceAsync(string sdpMid, int sdpMLineIndex, string candidate, string clientId = "")
    {
        var msg = new Dictionary<string, object> {
            { "type", "ice" },
            { "candidate", candidate },
            { "sdpMLineIndex", sdpMLineIndex },
            { "sdpMid", sdpMid }
        };
        if (isServer) msg["clientID"] = clientId;

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