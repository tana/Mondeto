using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

// This class hides some unsafe things related to MsQuic and provide some utility functions
unsafe class QuicLibrary
{
    public static QUIC_API_TABLE* ApiTable = null;
    public static QUIC_HANDLE* Registration = null;

    public static bool IsReady { get => ApiTable != null; }

    // Initialize MsQuic library
    public static void Initialize()
    {
        ApiTable = MsQuic.Open();

        // Create a registration with default config
        fixed (QUIC_HANDLE** registrationAddr = &Registration)
        {
            MsQuic.ThrowIfFailure(
                ApiTable->RegistrationOpen(null, registrationAddr)
            );
        }
    }

    public static void Close()
    {
        if (ApiTable != null)
        {
            if (Registration != null)
            {
                ApiTable->RegistrationClose(Registration);
            }

            MsQuic.Close(ApiTable);
        }
    }

    public static QuicAddr EndPointToQuicAddr(IPEndPoint ep)
    {
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

        return addr;
    }

    public static IPEndPoint QuicAddrToEndPoint(QuicAddr addr)
    {
        if (addr.Family == MsQuic.QUIC_ADDRESS_FAMILY_INET) // IPv4
        {
            byte[] addrBytes = new byte[4];
            Marshal.Copy((IntPtr)addr.Ipv4.sin_addr, addrBytes, 0, 4);
            return new IPEndPoint(new IPAddress(addrBytes), IPAddress.NetworkToHostOrder((short)addr.Ipv4.sin_port));
        }
        else if (addr.Family == MsQuic.QUIC_ADDRESS_FAMILY_INET6)   // IPv6
        {
            byte[] addrBytes = new byte[16];
            Marshal.Copy((IntPtr)addr.Ipv6.sin6_addr, addrBytes, 0, 16);
            return new IPEndPoint(new IPAddress(addrBytes), IPAddress.NetworkToHostOrder((short)addr.Ipv6.sin6_port));
        }
        else
        {
            throw new ArgumentException("Unknown address family");
        }
    }
}

}   // end namespace