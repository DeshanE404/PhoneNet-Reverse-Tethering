using System.Text.RegularExpressions;

namespace PhoneNet.Core.Android;

public sealed record AndroidDeviceInfo(
    string SerialNumber,
    string State,
    string? Product,
    string? Model,
    string? Device,
    string? TransportId);

public sealed record AndroidDetectionResult(
    bool IsAdbAvailable,
    IReadOnlyList<AndroidDeviceInfo> Devices,
    string Message);

public sealed class AndroidDeviceDetector
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);
    private readonly AdbProcessRunner adb;

    public AndroidDeviceDetector(string? adbPath = null)
    {
        adb = new AdbProcessRunner(adbPath);
    }

    public async Task<bool> IsAdbAvailableAsync()
    {
        var result = await adb.RunAsync("version");
        return result.Started && result.ExitCode == 0;
    }

    public async Task<AndroidDetectionResult> DetectDevicesAsync()
    {
        var adbCheck = await adb.RunAsync("version");
        if (!adbCheck.Started)
        {
            return new AndroidDetectionResult(false, [],
                "ADB was not found. Install Android SDK Platform Tools and add adb.exe to PATH or set PHONENET_ADB_PATH.");
        }

        if (adbCheck.ExitCode != 0)
        {
            return new AndroidDetectionResult(false, [], "ADB is installed but could not be started.");
        }

        var server = await adb.RunAsync("start-server");
        if (!server.Started || server.ExitCode != 0)
        {
            return new AndroidDetectionResult(true, [], "ADB server could not be started.");
        }

        var devices = await adb.RunAsync("devices", "-l");
        if (!devices.Started)
        {
            return new AndroidDetectionResult(true, [], "ADB device detection could not be started.");
        }

        if (devices.TimedOut)
        {
            return new AndroidDetectionResult(true, [], "ADB device detection timed out.");
        }

        var parsedDevices = ParseDevices(devices.Output);
        return new AndroidDetectionResult(true, parsedDevices,
            parsedDevices.Count == 0 ? "No Android devices detected." : "Android device detection completed.");
    }

    private static IReadOnlyList<AndroidDeviceInfo> ParseDevices(string output)
    {
        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseDevice)
            .Where(device => device is not null)
            .Cast<AndroidDeviceInfo>()
            .ToArray();
    }

    private static AndroidDeviceInfo? ParseDevice(string line)
    {
        var fields = Regex.Split(line.Trim(), "\\s+");
        if (fields.Length < 2 || fields[1] is not ("device" or "unauthorized" or "offline"))
        {
            return null;
        }

        var attributes = fields.Skip(2)
            .Select(field => field.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        attributes.TryGetValue("product", out var product);
        attributes.TryGetValue("model", out var model);
        attributes.TryGetValue("device", out var device);
        attributes.TryGetValue("transport_id", out var transportId);
        return new AndroidDeviceInfo(fields[0], fields[1], product, model, device, transportId);
    }

}
