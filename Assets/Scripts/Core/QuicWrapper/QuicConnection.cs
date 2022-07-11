using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicConnection : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CallbackDelegate(QUIC_HANDLE* connection, void* context, QUIC_CONNECTION_EVENT* evt);

    public delegate void DatagramReceivedEventHandler(QuicConnection connection, byte[] data);
    public delegate void ConnectedEventHandler(QuicConnection connection);
    public delegate void PeerStreamStartedEventHandler(QuicConnection connection, QuicStream stream);

    public event DatagramReceivedEventHandler DatagramReceived;
    public event ConnectedEventHandler Connected;
    public event PeerStreamStartedEventHandler PeerStreamStarted;

    public QUIC_HANDLE* Handle = null;

    QUIC_TLS_SECRETS* secretsPtr = null;

    static ConcurrentDictionary<IntPtr, QuicConnection> instances = new();

    static readonly CallbackDelegate cbDelegate = Callback; // This delegate is never garbage-collected

    // Create a QuicConnection from scratch (for clients)
    public QuicConnection(bool enableKeyLog = false)
    {
        var cbPtr = (delegate* unmanaged[Cdecl]<QUIC_HANDLE*, void*, QUIC_CONNECTION_EVENT*, int>)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);

        fixed (QUIC_HANDLE** handleAddr = &Handle)
        {
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ConnectionOpen(QuicLibrary.Registration, cbPtr, null, handleAddr)
            );
        }

        instances[(IntPtr)Handle] = this;

        if (enableKeyLog)
        {
            secretsPtr = (QUIC_TLS_SECRETS*)Marshal.AllocHGlobal(sizeof(QUIC_TLS_SECRETS));
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->SetParam(
                    Handle, MsQuic.QUIC_PARAM_CONN_TLS_SECRETS,
                    (uint)sizeof(QUIC_TLS_SECRETS), secretsPtr
                )
            );
        }
    }

    // Create a QuicConnection from an existing handle (used in servers to accept clients)
    public QuicConnection(QUIC_HANDLE* handle)
    {
        Handle = handle;

        var cbPtr = (void*)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);
        QuicLibrary.ApiTable->SetCallbackHandler(Handle, cbPtr, null);

        instances[(IntPtr)Handle] = this;
    }

    public void Dispose()
    {
        //Logger.Debug("QuicConnection", "Closing");

        if (secretsPtr != null)
        {
            Marshal.FreeHGlobal((IntPtr)secretsPtr);
        }

        if (Handle != null)
        {
            QuicLibrary.ApiTable->ConnectionClose(Handle);

            instances.TryRemove((IntPtr)Handle, out _);
        }
    }

    public void Start(byte[][] alpns, string serverName, int serverPort, bool noCertValidation = false)
    {
        using var config = QuicConfiguration.CreateClientConfiguration(alpns, true, 2, noCertValidation);

        fixed (byte* serverNameCStr = QuicLibrary.ToCString(serverName))
        {
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ConnectionStart(
                    Handle,
                    config.Handle,
                    (ushort)MsQuic.QUIC_ADDRESS_FAMILY_UNSPEC,
                    (sbyte*)serverNameCStr, (ushort)serverPort
                )
            );
        }
    }

    public void SendDatagram(byte[] data)
    {
        // Dynamically allocate QUIC_BUFFER structure along with buffer for content
        // https://github.com/microsoft/msquic/blob/47ee814b4dc0d113d983f9bc71222ba4025c2825/src/tools/sample/sample.c#L203
        // Note: Placing QUIC_BUFFER structure on stack and dynamically allocating only content buffer results in a crash.
        QUIC_BUFFER* buf = (QUIC_BUFFER*)Marshal.AllocHGlobal(sizeof(QUIC_BUFFER) + data.Length);
        buf->Buffer = (byte*)buf + sizeof(QUIC_BUFFER);
        buf->Length = (uint)data.Length;

        Marshal.Copy(data, 0, (IntPtr)buf->Buffer, data.Length);

        // The memory allocated above will be released in the event handler
        // because it is passed as a client context (last argument of DatagramSend).
        // https://github.com/microsoft/msquic/blob/47ee814b4dc0d113d983f9bc71222ba4025c2825/src/tools/sample/sample.c#L248
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->DatagramSend(
                Handle,
                buf, 1,
                QUIC_SEND_FLAGS.QUIC_SEND_FLAG_NONE,
                buf // client context
            )
        );
    }

    public QuicStream OpenStream()
    {
        QUIC_HANDLE* stream;
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->StreamOpen(
                Handle,
                QUIC_STREAM_OPEN_FLAGS.QUIC_STREAM_OPEN_FLAG_NONE,  // bidirectional (default)
                QuicStream.CallbackFunctionPointer, null,
                &stream
            )
        );
        return new QuicStream(stream, false);
    }

    // Get TLS secrets of current connection as SSLKEYLOGFILE format
    // (Reference: https://firefox-source-docs.mozilla.org/security/nss/legacy/key_log_format/index.html )
    public string GetKeyLog()
    {
        if (secretsPtr == null)
        {
            throw new NotSupportedException();
        }

        string clientRandom = BytesToHex(new Span<byte>(secretsPtr->ClientRandom, 32));

        StringBuilder sb = new();
        if (secretsPtr->IsSet.ClientEarlyTrafficSecret != 0)
        {
            sb.Append($"CLIENT_EARLY_TRAFFIC_SECRET {clientRandom} {BytesToHex(new Span<byte>(secretsPtr->ClientEarlyTrafficSecret, secretsPtr->SecretLength))}\n");
        }
        if (secretsPtr->IsSet.ClientHandshakeTrafficSecret != 0)
        {
            sb.Append($"CLIENT_HANDSHAKE_TRAFFIC_SECRET {clientRandom} {BytesToHex(new Span<byte>(secretsPtr->ClientHandshakeTrafficSecret, secretsPtr->SecretLength))}\n");
        }
        if (secretsPtr->IsSet.ServerHandshakeTrafficSecret != 0)
        {
            sb.Append($"SERVER_HANDSHAKE_TRAFFIC_SECRET {clientRandom} {BytesToHex(new Span<byte>(secretsPtr->ServerHandshakeTrafficSecret, secretsPtr->SecretLength))}\n");
        }
        if (secretsPtr->IsSet.ClientTrafficSecret0 != 0)
        {
            sb.Append($"CLIENT_TRAFFIC_SECRET_0 {clientRandom} {BytesToHex(new Span<byte>(secretsPtr->ClientTrafficSecret0, secretsPtr->SecretLength))}\n");
        }
        if (secretsPtr->IsSet.ServerTrafficSecret0 != 0)
        {
            sb.Append($"SERVER_TRAFFIC_SECRET_0 {clientRandom} {BytesToHex(new Span<byte>(secretsPtr->ServerTrafficSecret0, secretsPtr->SecretLength))}\n");
        }

        return sb.ToString();
    }

    string BytesToHex(Span<byte> data)
    {
        StringBuilder sb = new();

        foreach (byte b in data)
        {
            sb.Append($"{b:x2}");
        }

        return sb.ToString();
    }

    int HandleEvent(QUIC_CONNECTION_EVENT* evt)
    {
        //Logger.Debug("QuicConnection", $"{(IntPtr)Handle} {evt->Type}");
        switch (evt->Type)
        {
            case QUIC_CONNECTION_EVENT_TYPE.QUIC_CONNECTION_EVENT_CONNECTED:
                Connected?.Invoke(this);
                break;

            case QUIC_CONNECTION_EVENT_TYPE.QUIC_CONNECTION_EVENT_DATAGRAM_SEND_STATE_CHANGED:
                if (evt->DATAGRAM_SEND_STATE_CHANGED.State == QUIC_DATAGRAM_SEND_STATE.QUIC_DATAGRAM_SEND_SENT || evt->DATAGRAM_SEND_STATE_CHANGED.State == QUIC_DATAGRAM_SEND_STATE.QUIC_DATAGRAM_SEND_CANCELED)
                {
                    // Buffer allocated to send a datagram is no longer needed
                    Marshal.FreeHGlobal((IntPtr)evt->DATAGRAM_SEND_STATE_CHANGED.ClientContext);
                }
                break;

            case QUIC_CONNECTION_EVENT_TYPE.QUIC_CONNECTION_EVENT_DATAGRAM_RECEIVED:
                byte[] data = new byte[evt->DATAGRAM_RECEIVED.Buffer->Length];
                Marshal.Copy((IntPtr)evt->DATAGRAM_RECEIVED.Buffer->Buffer, data, 0, data.Length);
                DatagramReceived?.Invoke(this, data);
                break;
            
            case QUIC_CONNECTION_EVENT_TYPE.QUIC_CONNECTION_EVENT_PEER_STREAM_STARTED:
                var stream = new QuicStream(evt->PEER_STREAM_STARTED.Stream, true);
                PeerStreamStarted?.Invoke(this, stream);
                break;
        }

        return MsQuic.QUIC_STATUS_SUCCESS;
    }

    // If this function is marked with UnmanagedCallersOnly attribute, it would be much easier to pass to MsQuic functions.
    // However, Unity does not seem to support this attribute.
    static int Callback(QUIC_HANDLE* connection, void* context, QUIC_CONNECTION_EVENT* evt)
    {
        return instances[(IntPtr)connection].HandleEvent(evt);
    }
}

}   // end namespace