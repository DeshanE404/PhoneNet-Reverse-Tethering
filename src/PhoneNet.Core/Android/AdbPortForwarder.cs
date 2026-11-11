namespace PhoneNet.Core.Android;

public sealed record AdbReverseRule(string SerialNumber, int RemotePort, int LocalPort);

public sealed record AdbOperationResult(bool Success, string Message);

public sealed class AdbPortForwarder
{
    private readonly AdbProcessRunner adb;

    public AdbPortForwarder(string? adbPath = null)
    {
        adb = new AdbProcessRunner(adbPath);
    }

    public async Task<AdbOperationResult> CreateReverseAsync(string serial, int remotePort = 27183, int localPort = 27183)
    {
        var result = await adb.RunAsync("-s", serial, "reverse", $"tcp:{remotePort}", $"tcp:{localPort}");
        return Result(result, "Reverse forwarding created.");
    }

    public async Task<IReadOnlyList<string>> ListReverseAsync(string serial)
    {
        var result = await adb.RunAsync("-s", serial, "reverse", "--list");
        return result.Started && result.ExitCode == 0
            ? result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : [];
    }

    public async Task<bool> ExistsAsync(string serial, int remotePort = 27183, int localPort = 27183)
    {
        var rules = await ListReverseAsync(serial);
        return rules.Any(rule => rule.Contains($"tcp:{remotePort}", StringComparison.OrdinalIgnoreCase)
            && rule.Contains($"tcp:{localPort}", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AdbOperationResult> RemoveReverseAsync(string serial, int remotePort = 27183)
    {
        var result = await adb.RunAsync("-s", serial, "reverse", "--remove", $"tcp:{remotePort}");
        return Result(result, "Reverse forwarding removed.");
    }

    private static AdbOperationResult Result(AdbCommandResult result, string successMessage) =>
        result.TimedOut
            ? new(false, "ADB command timed out.")
            : !result.Started
                ? new(false, "ADB could not be started.")
                : result.ExitCode == 0
                    ? new(true, successMessage)
                    : new(false, string.IsNullOrWhiteSpace(result.Output) ? "ADB reverse forwarding command failed." : result.Output.Trim());
}

