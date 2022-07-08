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
        QuicAddr addr = new();
        if (ep.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
        {
            addr.Family = MsQuic.QUIC_ADDRESS_FAMILY_INET;
            Marshal.Copy(ep.Address.GetAddressBytes(), 0, (IntPtr)addr.Ipv4.sin_addr, 4);
            addr.Ipv4.sin_port = (ushort)IPAddress.HostToNetworkOrder((short)ep.Port);
        } else if (ep.Address.AddressFamily == AddressFamily.InterNetworkV6)    // IPv6
        {
            addr.Family = MsQuic.QUIC_ADDRESS_FAMILY_INET6;
            Marshal.Copy(ep.Address.GetAddressBytes(), 0, (IntPtr)addr.Ipv6.sin6_addr, 16);
            addr.Ipv6.sin6_port = (ushort)IPAddress.HostToNetworkOrder((short)ep.Port);
        }

        MsQuic.ThrowIfFailure(
            QuicLibrary.ApiTable->ListenerStart(handle, alpnBuffers, (uint)alpns.Length, &addr)
        );
    }

    int HandleEvent(QUIC_LISTENER_EVENT* evt)
    {
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