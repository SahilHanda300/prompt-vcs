using System.Net.WebSockets;
using System.Text;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Bridges a browser WebSocket to a spawned `pvcs` process (the same
/// interactive REPL built for local use), so the CLI can be driven from
/// `wwwroot/terminal.html`'s xterm.js terminal. Not a real PTY: the CLI's
/// interactive mode is a plain line-based read/print loop, so the browser
/// does its own minimal line-editing and each WebSocket message is one
/// already-typed line; the server just pumps stdio bytes both ways.
/// </summary>
public class TerminalSessionManager
{
    private const int MaxConcurrentSessions = 3;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _sessionSlots = new(MaxConcurrentSessions, MaxConcurrentSessions);
    private readonly string _cliDllPath;
    private readonly int _port;

    public TerminalSessionManager(string cliDllPath, int port)
    {
        _cliDllPath = cliDllPath;
        _port = port;
    }

    public async Task RunSessionAsync(WebSocket socket, CancellationToken ct)
    {
        if (!await _sessionSlots.WaitAsync(0, ct))
        {
            await SendTextAsync(socket, "\r\nServer busy — too many terminal sessions already open. Try again shortly.\r\n", ct);
            await CloseQuietlyAsync(socket, "Server busy");
            return;
        }

        try
        {
            await RunProcessBridgeAsync(socket, ct);
        }
        finally
        {
            _sessionSlots.Release();
        }
    }

    private async Task RunProcessBridgeAsync(WebSocket socket, CancellationToken outerCt)
    {
        // Every browser terminal session spawns a CLI process on this same
        // server machine, so a shared session-cache path (the CLI's normal
        // %USERPROFILE% default) would let one browser tab's login leak into
        // another's. Each session gets its own throwaway file instead —
        // deleted once the session ends.
        var sessionFilePath = Path.Combine(Path.GetTempPath(), "promptvcs-terminal-sessions", $"{Guid.NewGuid():N}.json");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(_cliDllPath);
        psi.Environment["PROMPTVCS_MCP_URL"] = $"http://localhost:{_port}/mcp";
        psi.Environment["PROMPTVCS_SESSION_PATH"] = sessionFilePath;

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            await SendTextAsync(socket, $"\r\nFailed to start terminal session: {ex.Message}\r\n", outerCt);
            await CloseQuietlyAsync(socket, "Failed to start");
            return;
        }

        using var idleCts = new CancellationTokenSource(IdleTimeout);
        // Cancelled by: the outer request aborting, the idle timer, or the
        // input pump noticing the browser sent a WebSocket close frame —
        // any one of these means the session is over.
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt, idleCts.Token);
        var ct = sessionCts.Token;

        void ResetIdleTimer()
        {
            try { idleCts.CancelAfter(IdleTimeout); } catch (ObjectDisposedException) { /* session already tearing down */ }
        }

        var outputTask = PumpProcessOutputToSocketAsync(process.StandardOutput.BaseStream, socket, ResetIdleTimer, ct);
        var errorTask = PumpProcessOutputToSocketAsync(process.StandardError.BaseStream, socket, ResetIdleTimer, ct);
        var inputTask = PumpSocketToProcessInputAsync(socket, process.StandardInput, ResetIdleTimer, sessionCts, ct);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // ct cancelled before the process exited on its own — kill it.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        // The process is gone one way or another at this point, but the
        // input pump is likely still blocked in socket.ReceiveAsync waiting
        // for the *browser* to send something. Cancelling that token would
        // abort the WebSocket outright (no clean handshake, 1006 on the
        // client) rather than close it — .NET's WebSocket doesn't treat a
        // cancelled receive as a graceful close. So: send a real close frame
        // first via CloseOutputAsync (safe to call alongside a pending
        // ReceiveAsync); the browser's automatic close-frame reply then
        // completes the input pump's receive loop the normal way, through
        // the existing "MessageType.Close" handling below. Only cancel
        // outright as a fallback if that doesn't wrap up promptly.
        await CloseQuietlyAsync(socket, "Session ended");
        sessionCts.CancelAfter(TimeSpan.FromSeconds(2));
        await Task.WhenAll(outputTask, errorTask, inputTask).ContinueWith(_ => { }, TaskScheduler.Default);

        try { File.Delete(sessionFilePath); } catch { /* best effort — nothing sensitive survives a temp dir cleanup either way */ }
    }

    private static async Task PumpProcessOutputToSocketAsync(Stream source, WebSocket socket, Action onActivity, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await source.ReadAsync(buffer, ct);
                if (read == 0) break; // process closed this stream (e.g. exited)
                onActivity();
                if (socket.State != WebSocketState.Open) break;
                await socket.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException) { /* session tearing down */ }
        catch (WebSocketException) { /* socket gone */ }
        catch (ObjectDisposedException) { /* stream/socket already torn down */ }
    }

    private static async Task PumpSocketToProcessInputAsync(WebSocket socket, StreamWriter input, Action onActivity, CancellationTokenSource sessionCts, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // The browser tab closed / navigated away — end the
                    // session now rather than waiting out the idle timeout.
                    sessionCts.Cancel();
                    break;
                }
                onActivity();
                var line = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await input.WriteAsync(line + "\n");
                await input.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* session tearing down */ }
        catch (WebSocketException) { /* socket gone */ }
        catch (IOException) { /* process's stdin already closed */ }
        catch (ObjectDisposedException) { /* stream/socket already torn down */ }
    }

    private static async Task SendTextAsync(WebSocket socket, string text, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        catch (WebSocketException) { /* best effort */ }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket, string reason)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
        try
        {
            // CloseOutputAsync, not CloseAsync: CloseAsync waits for the
            // peer's close-frame reply itself, which conflicts with the
            // input pump's own concurrently-pending ReceiveAsync on the same
            // socket. CloseOutputAsync only sends and returns immediately,
            // leaving the existing receive loop to observe the browser's
            // reply as a normal MessageType.Close.
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
        }
        catch { /* best effort — client may already be gone */ }
    }
}
