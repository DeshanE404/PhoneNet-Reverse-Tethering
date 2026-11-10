using System.Diagnostics;
using System.Net.Http;

namespace PhoneNet.Core.Networking;

public sealed record InternetConnectivityResult(
    bool IsConnected,
    TimeSpan? ResponseTime,
    string Message);

public static class InternetConnectivityChecker
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private const string TestUrl = "https://www.msftconnecttest.com/connecttest.txt";
    private const string ExpectedContent = "Microsoft Connect Test";

    public static async Task<InternetConnectivityResult> CheckAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await HttpClient.GetAsync(TestUrl);
            var content = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new InternetConnectivityResult(
                    false,
                    null,
                    $"The connectivity test returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            if (!content.Contains(ExpectedContent, StringComparison.OrdinalIgnoreCase))
            {
                return new InternetConnectivityResult(
                    false,
                    null,
                    "The response did not match the expected connectivity test content.");
            }

            return new InternetConnectivityResult(true, stopwatch.Elapsed, "Internet access is available.");
        }
        catch (OperationCanceledException)
        {
            return new InternetConnectivityResult(false, null, "The connectivity test timed out after 5 seconds.");
        }
        catch (HttpRequestException exception)
        {
            return new InternetConnectivityResult(false, null, GetRequestErrorMessage(exception));
        }
        catch (Exception)
        {
            return new InternetConnectivityResult(false, null, "The connectivity test could not be completed.");
        }
    }

    private static string GetRequestErrorMessage(HttpRequestException exception)
    {
        return exception.InnerException is System.Net.Sockets.SocketException
            ? "DNS resolution or network connection failed."
            : "The internet connection is unavailable.";
    }
}
