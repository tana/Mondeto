using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicConnection : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CallbackDelegate(QUIC_HANDLE* connection, void* context, QUIC_CONNECTION_EVENT* evt);

    public delegate void DatagramReceivedEventHandler(byte[] data);

    public event DatagramReceivedEventHandler DatagramReceived;

    public QUIC_HANDLE* Handle = null;

    static ConcurrentDictionary<IntPtr, QuicConnection> instances = new();

    static readonly CallbackDelegate cbDelegate = Callback; // This delegate is never garbage-collected

    public QuicConnection(QUIC_HANDLE* handle)
    {
        Handle = handle;

        var cbPtr = (void*)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);
        QuicLibrary.ApiTable->SetCallbackHandler(Handle, cbPtr, null);

        instances[(IntPtr)Handle] = this;
    }

    public void Dispose()
    {
        if (Handle != null)
        {
            QuicLibrary.ApiTable->ConnectionClose(Handle);

            instances.TryRemove((IntPtr)Handle, out _);
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
                buf->Buffer // client context
            )
        );
    }

    int HandleEvent(QUIC_CONNECTION_EVENT* evt)
    {
        switch (evt->Type)
        {
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
                DatagramReceived?.Invoke(data);
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