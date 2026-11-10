using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PhoneNet.Core.Networking;

public sealed record NetworkAdapterInfo(
    string Name,
    string Description,
    OperationalStatus OperationalStatus,
    string MacAddress,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> IPv4GatewayAddresses,
    IReadOnlyList<string> IPv4DnsServerAddresses);

public static class EthernetDetector
{
    public static IReadOnlyList<NetworkAdapterInfo> GetEthernetAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .Select(adapter =>
            {
                var properties = adapter.GetIPProperties();

                return new NetworkAdapterInfo(
                    adapter.Name,
                    adapter.Description,
                    adapter.OperationalStatus,
                    adapter.GetPhysicalAddress().ToString(),
                    properties.UnicastAddresses
                        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(address => address.Address.ToString())
                        .ToArray(),
                    properties.GatewayAddresses
                        .Where(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(gateway => gateway.Address.ToString())
                        .ToArray(),
                    properties.DnsAddresses
                        .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(address => address.ToString())
                        .ToArray());
            })
            .OrderBy(adapter => adapter.Name)
            .ToArray();
    }
}
