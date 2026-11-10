using PhoneNet.Core.Android;
using PhoneNet.Core.Networking;

while (true)
{
    Console.Clear();
    Console.WriteLine("=================================");
    Console.WriteLine("          PHONENET v0.1");
    Console.WriteLine("=================================");
    Console.WriteLine();
    Console.WriteLine("1. Detect Ethernet adapters");
    Console.WriteLine("2. Test Internet Connectivity");
    Console.WriteLine("3. Detect Android Devices");
    Console.WriteLine("4. Exit");
    Console.WriteLine();
    Console.Write("Select an option: ");

    switch (Console.ReadLine())
    {
        case "1":
            DisplayEthernetAdapters();
            break;
        case "2":
            await TestInternetConnectivityAsync();
            break;
        case "3":
            await DetectAndroidDevicesAsync();
            break;
        case "4":
            return;
        default:
            Console.WriteLine("Invalid selection.");
            Pause();
            break;
    }
}

static void DisplayEthernetAdapters()
{
    Console.WriteLine();
    var adapters = EthernetDetector.GetEthernetAdapters();

    if (adapters.Count == 0)
    {
        Console.WriteLine("No Ethernet adapters found.");
        Pause();
        return;
    }

    foreach (var adapter in adapters)
    {
        Console.WriteLine($"Name: {adapter.Name}");
        Console.WriteLine($"Description: {adapter.Description}");
        Console.WriteLine($"Status: {adapter.OperationalStatus}");
        Console.WriteLine($"MAC address: {Display(adapter.MacAddress)}");
        Console.WriteLine($"IPv4 addresses: {DisplayList(adapter.IPv4Addresses)}");
        Console.WriteLine($"IPv4 gateways: {DisplayList(adapter.IPv4GatewayAddresses)}");
        Console.WriteLine($"IPv4 DNS servers: {DisplayList(adapter.IPv4DnsServerAddresses)}");
        Console.WriteLine();
    }

    Pause();
}

static async Task TestInternetConnectivityAsync()
{
    Console.Clear();
    Console.WriteLine("========== INTERNET CONNECTIVITY TEST ==========");
    Console.WriteLine();

    var adapters = EthernetDetector.GetEthernetAdapters();
    var activeAdapters = adapters
        .Where(adapter => adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
        .ToArray();

    if (activeAdapters.Length == 0)
    {
        Console.WriteLine("Warning: No active Ethernet adapter is available.");
    }
    else
    {
        foreach (var adapter in activeAdapters)
        {
            Console.WriteLine($"Ethernet Adapter : {adapter.Name}");
            Console.WriteLine($"Adapter Status   : {adapter.OperationalStatus}");
            Console.WriteLine($"IPv4 Address     : {DisplayList(adapter.IPv4Addresses)}");
            Console.WriteLine($"Gateway          : {DisplayList(adapter.IPv4GatewayAddresses)}");
            Console.WriteLine();
        }
    }

    Console.WriteLine("Testing internet connectivity...");
    Console.WriteLine();

    var result = await InternetConnectivityChecker.CheckAsync();
    Console.WriteLine($"Internet Status  : {(result.IsConnected ? "CONNECTED" : "NOT CONNECTED")}");
    if (result.ResponseTime is { } responseTime)
    {
        Console.WriteLine($"Response Time    : {responseTime.TotalMilliseconds:0} ms");
    }

    Console.WriteLine();
    Console.WriteLine(result.Message);
    Pause();
}

static async Task DetectAndroidDevicesAsync()
{
    Console.Clear();
    Console.WriteLine("========== ANDROID DEVICE DETECTION ==========");
    Console.WriteLine();
    Console.WriteLine("Searching for Android devices...");
    Console.WriteLine();

    var result = await new AndroidDeviceDetector().DetectDevicesAsync();
    if (!result.IsAdbAvailable)
    {
        Console.WriteLine(result.Message);
        Pause();
        return;
    }

    if (result.Devices.Count == 0)
    {
        Console.WriteLine(result.Message);
        Console.WriteLine();
        Console.WriteLine("Please check:");
        Console.WriteLine("- USB data cable connection.");
        Console.WriteLine("- USB debugging is enabled.");
        Console.WriteLine("- Android phone is unlocked.");
        Console.WriteLine("- USB debugging authorization is granted.");
        Pause();
        return;
    }

    for (var index = 0; index < result.Devices.Count; index++)
    {
        var device = result.Devices[index];
        Console.WriteLine($"Device {index + 1}");
        Console.WriteLine();
        Console.WriteLine($"Device ID       : {device.SerialNumber}");
        Console.WriteLine($"Model           : {Display(device.Model)}");
        Console.WriteLine($"Product         : {Display(device.Product)}");
        Console.WriteLine($"Device Name     : {Display(device.Device)}");
        Console.WriteLine($"Transport ID    : {Display(device.TransportId)}");
        Console.WriteLine($"Device State    : {device.State}");
        Console.WriteLine($"Status          : {GetDeviceStatus(device.State)}");
        if (device.State == "unauthorized")
        {
            Console.WriteLine();
            Console.WriteLine("Please unlock your Android phone and allow the USB debugging authorization request.");
        }
        Console.WriteLine();
    }

    Console.WriteLine("Android device detection completed.");
    Pause();
}

static string GetDeviceStatus(string state) => state switch
{
    "device" => "READY",
    "unauthorized" => "ACTION REQUIRED",
    "offline" => "OFFLINE",
    _ => "UNKNOWN"
};

static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "None available" : value;

static string DisplayList(IReadOnlyList<string> values) => values.Count == 0 ? "None assigned" : string.Join(", ", values);

static void Pause()
{
    Console.WriteLine();
    Console.Write("Press Enter to continue...");
    Console.ReadLine();
}
