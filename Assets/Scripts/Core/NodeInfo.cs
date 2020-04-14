using System.Collections.Generic;
using System.Net;

public class NodeInfo
{
    public IPAddress Address { get; }
    public int UdpPort { get; }
    public int TcpPort { get; }

    public NodeInfo(IPAddress address, int udpPort, int tcpPort)
    {
        Address = address;
        UdpPort = udpPort;
        TcpPort = tcpPort;
    }

    public override string ToString()
    {
        return $"{Address},UDP={UdpPort},TCP={TcpPort}";
    }

    public override bool Equals(object obj)
    {
        var info = obj as NodeInfo;
        return info != null &&
               EqualityComparer<IPAddress>.Default.Equals(Address, info.Address) &&
               UdpPort == info.UdpPort &&
               TcpPort == info.TcpPort;
    }

    public override int GetHashCode()
    {
        var hashCode = 1749498485;
        hashCode = hashCode * -1521134295 + EqualityComparer<IPAddress>.Default.GetHashCode(Address);
        hashCode = hashCode * -1521134295 + UdpPort.GetHashCode();
        hashCode = hashCode * -1521134295 + TcpPort.GetHashCode();
        return hashCode;
    }
}