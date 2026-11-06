using System.Net.Sockets;
using System.Text;

namespace Bo3Rtm;

/// <summary>Line client for bo3-unlock.sprx (TCP 9027). Every command ends with a DONE line.</summary>
internal sealed class ConsoleClient : IDisposable
{
    public const int Port = 9027;

    private TcpClient? _tcp;
    private NetworkStream? _ns;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool Connected => _tcp?.Connected == true;

    public async Task ConnectAsync(string ip, CancellationToken ct = default)
    {
        Close();
        var tcp = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        await tcp.ConnectAsync(ip, Port, cts.Token).ConfigureAwait(false);
        _tcp = tcp;
        _ns = tcp.GetStream();
    }

    /// <summary>Sends one command and returns every line before DONE.</summary>
    public async Task<List<string>> SendAsync(string cmd, int timeoutSec = 60)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ns == null) throw new InvalidOperationException("not connected");
            var bytes = Encoding.ASCII.GetBytes(cmd + "\n");
            await _ns.WriteAsync(bytes).ConfigureAwait(false);

            var lines = new List<string>();
            var sb = new StringBuilder();
            var buf = new byte[1024];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            while (true)
            {
                int n = await _ns.ReadAsync(buf, cts.Token).ConfigureAwait(false);
                if (n <= 0) { Close(); throw new IOException("console closed the connection"); }
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                string s = sb.ToString();
                int nl;
                while ((nl = s.IndexOf('\n')) >= 0)
                {
                    string line = s[..nl].TrimEnd('\r');
                    s = s[(nl + 1)..];
                    if (line == "DONE") return lines;
                    lines.Add(line);
                }
                sb.Clear().Append(s);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"no reply to '{cmd}' in {timeoutSec}s");
        }
        finally { _gate.Release(); }
    }

    public void Close()
    {
        try { _ns?.Dispose(); } catch { }
        try { _tcp?.Dispose(); } catch { }
        _ns = null; _tcp = null;
    }

    public void Dispose() => Close();
}
