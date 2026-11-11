using System.Diagnostics;

namespace PhoneNet.Core.Android;

internal sealed record AdbCommandResult(bool Started, int ExitCode, string Output, bool TimedOut)
{
    public static AdbCommandResult NotStarted => new(false, -1, string.Empty, false);
}

internal sealed class AdbProcessRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly string? adbPath;

    public AdbProcessRunner(string? configuredPath = null)
    {
        adbPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Environment.GetEnvironmentVariable("PHONENET_ADB_PATH")
            : configuredPath;
    }

    public async Task<AdbCommandResult> RunAsync(params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath,
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
                return AdbCommandResult.NotStarted;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var cancellation = new CancellationTokenSource(Timeout);
            try
            {
                await process.WaitForExitAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return new AdbCommandResult(false, -1, string.Empty, true);
            }

            return new AdbCommandResult(true, process.ExitCode, await outputTask + await errorTask, false);
        }
        catch (Exception)
        {
            return AdbCommandResult.NotStarted;
        }
    }
}
