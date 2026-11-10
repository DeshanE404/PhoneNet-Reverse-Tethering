using System.Diagnostics;
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
    private readonly string? configuredAdbPath;

    public AndroidDeviceDetector(string? adbPath = null)
    {
        configuredAdbPath = string.IsNullOrWhiteSpace(adbPath)
            ? Environment.GetEnvironmentVariable("PHONENET_ADB_PATH")
            : adbPath;
    }

    public async Task<bool> IsAdbAvailableAsync()
    {
        var result = await RunAdbAsync("version");
        return result.Started && result.ExitCode == 0;
    }

    public async Task<AndroidDetectionResult> DetectDevicesAsync()
    {
        var adbCheck = await RunAdbAsync("version");
        if (!adbCheck.Started)
        {
            return new AndroidDetectionResult(false, [],
                "ADB was not found. Install Android SDK Platform Tools and add adb.exe to PATH or set PHONENET_ADB_PATH.");
        }

        if (adbCheck.ExitCode != 0)
        {
            return new AndroidDetectionResult(false, [], "ADB is installed but could not be started.");
        }

        var server = await RunAdbAsync("start-server");
        if (!server.Started || server.ExitCode != 0)
        {
            return new AndroidDetectionResult(true, [], "ADB server could not be started.");
        }

        var devices = await RunAdbAsync("devices", "-l");
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

    private async Task<ProcessResult> RunAdbAsync(params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(configuredAdbPath) ? "adb" : configuredAdbPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            if (!process.Start())
            {
                return ProcessResult.NotStarted;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(ProcessTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return new ProcessResult(false, -1, string.Empty, true);
            }

            return new ProcessResult(true, process.ExitCode, await outputTask + await errorTask, false);
        }
        catch (Exception)
        {
            return ProcessResult.NotStarted;
        }
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

    private sealed record ProcessResult(bool Started, int ExitCode, string Output, bool TimedOut)
    {
        public static ProcessResult NotStarted => new(false, -1, string.Empty, false);
    }
}
