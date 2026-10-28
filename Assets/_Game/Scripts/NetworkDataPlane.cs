using System;
using System.Collections.Generic;

namespace NetworkCollapse
{
    // Data-plane entities. Protocol control planes are deliberately not simulated here.
    public enum InterfaceMode { Routed, Access, Trunk }
    [Serializable] public sealed class NetworkInterface
    {
        public string Name;
        public int NodeId;
        public int LinkIndex;
        public string Ipv4Address;
        public int PrefixLength = 31;
        public int VlanId = 1;
        public int Mtu = 1500;
        public bool AdminUp = true;
        public bool OperationalUp = true;
        public InterfaceMode Mode;
        public string LagGroup;
    }
    [Serializable] public sealed class VLAN
    {
        public int Id;
        public string Name;
        public List<int> MemberNodes = new List<int>();
    }
    [Serializable] public sealed class Subnet
    {
        public string NetworkAddress;
        public int PrefixLength;
        public string Purpose;
    }
    [Serializable] public sealed class RouteEntry
    {
        public string Destination;
        public int PrefixLength;
        public string NextHop;
        public string OutInterface;
        public int Metric;
        public bool Active;
    }
    [Serializable] public sealed class RoutingTable
    {
        public int NodeId;
        public List<RouteEntry> Routes = new List<RouteEntry>();
    }
    [Serializable] public sealed class NeighborEntry
    {
        public string IpAddress;
        public string MacAddress;
        public string InterfaceName;
        public int AgeTicks;
    }
    [Serializable] public sealed class NeighborTable
    {
        public int NodeId;
        public List<NeighborEntry> Entries = new List<NeighborEntry>();
    }
    [Serializable] public sealed class SwitchingEntry
    {
        public string MacAddress;
        public int VlanId;
        public string InterfaceName;
        public int AgeTicks;
    }
    [Serializable] public sealed class SwitchingTable
    {
        public int NodeId;
        public List<SwitchingEntry> Entries = new List<SwitchingEntry>();
    }
}
