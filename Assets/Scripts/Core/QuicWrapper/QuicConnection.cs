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

    int HandleEvent(QUIC_CONNECTION_EVENT* evt)
    {
        Logger.Debug("QuicConnection", evt->Type.ToString());
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