using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicListener : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CallbackDelegate(QUIC_HANDLE* listener, void* context, QUIC_LISTENER_EVENT* evt);

    public delegate void ClientConnectedEventHandler(QuicConnection connection, IPEndPoint endPoint);
    
    public event ClientConnectedEventHandler ClientConnected;

    public QUIC_HANDLE* Handle = null;

    QuicConfiguration configuration;

    static ConcurrentDictionary<IntPtr, QuicListener> instances = new();

    static readonly CallbackDelegate cbDelegate = Callback; // This delegate is never garbage-collected

    public QuicListener()
    {
        var cbPtr = (delegate* unmanaged[Cdecl]<QUIC_HANDLE*, void*, QUIC_LISTENER_EVENT*, int>)Marshal.GetFunctionPointerForDelegate<CallbackDelegate>(cbDelegate);

        fixed (QUIC_HANDLE** handleAddr = &Handle)
        {
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ListenerOpen(QuicLibrary.Registration, cbPtr, null, handleAddr)
            );
        }

        instances[(IntPtr)Handle] = this;
    }

    public void Dispose()
    {
        if (configuration != null)
        {
            configuration.Dispose();
        }

        if (Handle != null)
        {
            QuicLibrary.ApiTable->ListenerClose(Handle);

            instances.TryRemove((IntPtr)Handle, out _);
        }
    }

    public void Start(byte[][] alpns, IPEndPoint ep, string privateKeyPath, string certificatePath)
    {
        // Create configuration for connections
        configuration = QuicConfiguration.CreateServerConfiguration(alpns, privateKeyPath, certificatePath, true, 1);

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
            QuicLibrary.ApiTable->ListenerStart(Handle, alpnBuffers, (uint)alpns.Length, &addr)
        );

        // Get IP address and port actually listening on
        QuicAddr localAddr;
        uint localAddrSize = (uint)sizeof(QuicAddr);
        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->GetParam(Handle, MsQuic.QUIC_PARAM_LISTENER_LOCAL_ADDRESS, &localAddrSize, &localAddr)
        );
        Logger.Debug("QuicListener", "Listening on " + QuicLibrary.QuicAddrToEndPoint(localAddr));
    }

    int HandleEvent(QUIC_LISTENER_EVENT* evt)
    {
        switch (evt->Type)
        {
            case QUIC_LISTENER_EVENT_TYPE.QUIC_LISTENER_EVENT_NEW_CONNECTION:
                IPEndPoint remoteEP = QuicLibrary.QuicAddrToEndPoint(*evt->NEW_CONNECTION.Info->RemoteAddress);
                QUIC_HANDLE* connectionHandle = evt->NEW_CONNECTION.Connection;
                Logger.Debug("QuicListener", "New connection from " + remoteEP);
                MsQuic.ThrowIfFailure(
                    QuicLibrary.ApiTable->ConnectionSetConfiguration(connectionHandle, configuration.Handle)
                );
                QuicConnection connection = new QuicConnection(connectionHandle);
                ClientConnected?.Invoke(connection, remoteEP);
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