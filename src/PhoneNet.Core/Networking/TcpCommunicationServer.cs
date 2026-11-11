using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PhoneNet.Core.Networking;

public sealed class TcpCommunicationServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 27183);
    private CancellationTokenSource? cancellation;
    private Task? acceptTask;

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            listener.Start();
            IsRunning = true;
            acceptTask = AcceptClientsAsync(cancellation.Token);
            return Task.CompletedTask;
        }
        catch (SocketException)
        {
            cancellation.Dispose();
            cancellation = null;
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        cancellation?.Cancel();
        listener.Stop();
        if (acceptTask is not null)
        {
            try { await acceptTask; } catch (OperationCanceledException) { }
        }

        IsRunning = false;
        cancellation?.Dispose();
        cancellation = null;
        acceptTask = null;
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(client, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
            catch (SocketException) when (!token.IsCancellationRequested) { }
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using var clientScope = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        while (!token.IsCancellationRequested)
        {
            string? message;
            try { message = await reader.ReadLineAsync(token); }
            catch (IOException) { return; }
            catch (OperationCanceledException) { return; }

            if (message is null) return;
            var response = message.Trim().ToUpperInvariant() switch
            {
                "PING" => "PONG",
                "HELLO" => "PHONENET_READY",
                _ => "UNKNOWN_COMMAND"
            };
            try { await writer.WriteLineAsync(response); }
            catch (IOException) { return; }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
