using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicListener : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CallbackDelegate(QUIC_HANDLE* listener, void* context, QUIC_LISTENER_EVENT* evt);

    QUIC_HANDLE* handle = null;

    static ConcurrentDictionary<IntPtr, QuicListener> instances = new();

    static readonly CallbackDelegate cbDelegate = Callback; // This delegate is never garbage-collected

    public QuicListener()
    {
        var cbPtr = (delegate* unmanaged[Cdecl]<QUIC_HANDLE*, void*, QUIC_LISTENER_EVENT*, int>)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);

        fixed (QUIC_HANDLE** handleAddr = &handle)
        {
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ListenerOpen(QuicLibrary.Registration, cbPtr, null, handleAddr)
            );
        }

        instances[(IntPtr)handle] = this;
    }

    public void Dispose()
    {
        if (handle != null)
        {
            QuicLibrary.ApiTable->ListenerClose(handle);

            instances.TryRemove((IntPtr)handle, out _);
        }
    }

    public void Start(byte[][] alpns, IPEndPoint ep)
    {
        // Convert ALPNs
        QUIC_BUFFER* alpnBuffers = stackalloc QUIC_BUFFER[alpns.Length];
        for (int i = 0; i < alpns.Length; i++)
        {
            byte* buf = stackalloc byte[alpns[i].Length];
            Marshal.Copy(alpns[i], 0, (IntPtr)buf, alpns[i].Length);
            alpnBuffers[i].Length = (uint)alpns[i].Length;
            alpnBuffers[i].Buffer = buf;
        }

        // Convert IP address and port
        QuicAddr addr = QuicLibrary.EndPointToQuicAddr(ep);

        // Start listener
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->ListenerStart(handle, alpnBuffers, (uint)alpns.Length, &addr)
        );

        // Get IP address and port actually listening on
        QuicAddr localAddr;
        uint localAddrSize = (uint)sizeof(QuicAddr);
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->GetParam(handle, MsQuic.QUIC_PARAM_LISTENER_LOCAL_ADDRESS, &localAddrSize, &localAddr)
        );
        Logger.Debug("QuicListener", "Listening on " + QuicLibrary.QuicAddrToEndPoint(localAddr));
    }

    int HandleEvent(QUIC_LISTENER_EVENT* evt)
    {
        switch (evt->Type)
        {
            case QUIC_LISTENER_EVENT_TYPE.QUIC_LISTENER_EVENT_NEW_CONNECTION:
                Logger.Debug("QuicListener", "New connection");
                break;
            case QUIC_LISTENER_EVENT_TYPE.QUIC_LISTENER_EVENT_STOP_COMPLETE:
                Logger.Debug("QuicListener", "Listener stop complete");
                break;
        }

        return MsQuic.QUIC_STATUS_SUCCESS;
    }

    // If this function is marked with UnmanagedCallersOnly attribute, it would be much easier to pass to MsQuic functions.
    // However, Unity does not seem to support this attribute.
    static int Callback(QUIC_HANDLE* listener, void* context, QUIC_LISTENER_EVENT* evt)
    {
        return instances[(IntPtr)listener].HandleEvent(evt);
    }
}

}   // end namespace