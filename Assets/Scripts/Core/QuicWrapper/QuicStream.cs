using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicStream : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CallbackDelegate(QUIC_HANDLE* stream, void* context, QUIC_STREAM_EVENT* evt);

    public delegate void ReceivedEventHandler(QuicStream stream, byte[] data);

    public event ReceivedEventHandler Received;

    public QUIC_HANDLE* Handle = null;

    static ConcurrentDictionary<IntPtr, QuicStream> instances = new();

    static readonly CallbackDelegate cbDelegate = Callback; // This delegate is never garbage-collected

    public static delegate* unmanaged[Cdecl]<QUIC_HANDLE*, void*, QUIC_STREAM_EVENT*, int> CallbackFunctionPointer
    {
        get => (delegate* unmanaged[Cdecl]<QUIC_HANDLE*, void*, QUIC_STREAM_EVENT*, int>)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);
    }

    public QuicStream(QUIC_HANDLE* handle, bool setCallback = true)
    {
        Handle = handle;

        if (setCallback)
        {
            QuicLibrary.ApiTable->SetCallbackHandler(Handle, CallbackFunctionPointer, null);
        }

        instances[(IntPtr)Handle] = this;
    }

    public void Dispose()
    {
        //Logger.Debug("QuicStream", "Closing");
        if (Handle != null)
        {
            QuicLibrary.ApiTable->StreamClose(Handle);

            instances.TryRemove((IntPtr)Handle, out _);
        }
    }

    public void Start(bool immediate = false)
    {
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->StreamStart(
                Handle,
                immediate ? QUIC_STREAM_START_FLAGS.QUIC_STREAM_START_FLAG_IMMEDIATE : QUIC_STREAM_START_FLAGS.QUIC_STREAM_START_FLAG_NONE
            )
        );
    }

    public void Send(byte[] data)
    {
        // Dynamically allocate QUIC_BUFFER structure along with buffer for content
        // https://github.com/microsoft/msquic/blob/47ee814b4dc0d113d983f9bc71222ba4025c2825/src/tools/sample/sample.c#L203
        QUIC_BUFFER* buf = (QUIC_BUFFER*)Marshal.AllocHGlobal(sizeof(QUIC_BUFFER) + data.Length);
        buf->Buffer = (byte*)buf + sizeof(QUIC_BUFFER);
        buf->Length = (uint)data.Length;

        Marshal.Copy(data, 0, (IntPtr)buf->Buffer, data.Length);

        // The memory allocated above will be released in the event handler
        // because it is passed as a client send context (last argument of StreamSend).
        // https://github.com/microsoft/msquic/blob/47ee814b4dc0d113d983f9bc71222ba4025c2825/src/tools/sample/sample.c#L248
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->StreamSend(
                Handle,
                buf, 1,
                QUIC_SEND_FLAGS.QUIC_SEND_FLAG_NONE,
                buf    // client send ocntext
            )
        );
    }

    int HandleEvent(QUIC_STREAM_EVENT* evt)
    {
        //Logger.Debug("QuicStream", $"{(IntPtr)Handle} {evt->Type}");
        switch (evt->Type)
        {
            case QUIC_STREAM_EVENT_TYPE.QUIC_STREAM_EVENT_SEND_COMPLETE:
                // Buffer allocated for sending is no longer needed
                Marshal.FreeHGlobal((IntPtr)evt->SEND_COMPLETE.ClientContext);
                break;
            
            case QUIC_STREAM_EVENT_TYPE.QUIC_STREAM_EVENT_RECEIVE:
                byte[] data = new byte[evt->RECEIVE.TotalBufferLength];
                uint pos = 0;
                for (uint i = 0; i < evt->RECEIVE.BufferCount; i++)
                {
                    Marshal.Copy((IntPtr)evt->RECEIVE.Buffers[i].Buffer, data, (int)pos, (int)evt->RECEIVE.Buffers[i].Length);
                    pos += evt->RECEIVE.Buffers[i].Length;
                }
                Received?.Invoke(this, data);
                break;
        }

        return MsQuic.QUIC_STATUS_SUCCESS;
    }

    // If this function is marked with UnmanagedCallersOnly attribute, it would be much easier to pass to MsQuic functions.
    // However, Unity does not seem to support this attribute.
    static int Callback(QUIC_HANDLE* stream, void* context, QUIC_STREAM_EVENT* evt)
    {
        return instances[(IntPtr)stream].HandleEvent(evt);
    }
}

}   // end namespace