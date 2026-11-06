using System.Drawing;

namespace Bo3Rtm;

internal sealed class MainForm : Form
{
    private readonly ConsoleClient _client = new();
    private readonly TextBox _ip = new();
    private readonly Label _status = new();
    private readonly TextBox _log = new();
    private readonly string _cfgPath = Path.Combine(AppContext.BaseDirectory, "bo3_rtm.ip");

    public MainForm()
    {
        Text = "BO3 Entitlements RTM";
        Size = new Size(760, 460);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(BuildLog());
        Controls.Add(BuildEntitlementsPanel());
        Controls.Add(BuildTopBar());
        FormClosing += (_, _) => _client.Dispose();
    }

    private Control BuildTopBar()
    {
        string ip = "";
        try { if (File.Exists(_cfgPath)) ip = File.ReadAllText(_cfgPath).Trim(); } catch { }

        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var label = new Label { Text = "Console IP", Location = new Point(12, 15), AutoSize = true };
        _ip.Text = ip;
        _ip.Location = new Point(82, 10);
        _ip.Width = 140;

        var connect = new Button { Text = "Connect", Location = new Point(232, 9), Size = new Size(90, 28) };
        connect.Click += async (_, _) => await ConnectAsync();
        var check = new Button { Text = "Check build", Location = new Point(328, 9), Size = new Size(100, 28) };
        check.Click += async (_, _) => await RunAsync("CHECK");
        var state = new Button { Text = "State", Location = new Point(434, 9), Size = new Size(70, 28) };
        state.Click += async (_, _) => await RunAsync("STATE");

        _status.Text = "disconnected";
        _status.ForeColor = Color.IndianRed;
        _status.AutoSize = true;
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Location = new Point(bar.Width - 110, 15);
        bar.Controls.AddRange(new Control[] { label, _ip, connect, check, state, _status });
        bar.Resize += (_, _) => _status.Location = new Point(bar.Width - _status.Width - 14, 15);
        return bar;
    }

    private Control BuildEntitlementsPanel()
    {
        var page = new Panel { Dock = DockStyle.Fill };
        var group = new GroupBox
        {
            Text = "Entitlements",
            Location = new Point(18, 18),
            Size = new Size(700, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var description = new Label
        {
            Text = "Writes BO3 key archive category 5, keys 1–25. Mod Tools calling card is key 25.",
            Location = new Point(18, 30),
            Size = new Size(650, 42)
        };
        var unlock = new Button
        {
            Text = Presets.Entitlements.Name,
            Location = new Point(18, 82),
            Size = new Size(210, 34)
        };
        unlock.Click += async (_, _) => await RunConfirmedAsync();
        group.Controls.AddRange(new Control[] { description, unlock });
        page.Controls.Add(group);
        page.Resize += (_, _) => group.Width = Math.Max(400, page.ClientSize.Width - 36);
        return page;
    }

    private Control BuildLog()
    {
        _log.Dock = DockStyle.Bottom;
        _log.Height = 190;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F);
        return _log;
    }

    private void Log(string line)
    {
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private async Task ConnectAsync()
    {
        string ip = _ip.Text.Trim();
        try { File.WriteAllText(_cfgPath, ip); } catch { }
        try
        {
            await _client.ConnectAsync(ip);
            SetStatus("connected", true);
            Log($"connected to {ip}:{ConsoleClient.Port}");
            await RunAsync("PING");
        }
        catch (Exception ex)
        {
            SetStatus("disconnected", false);
            Log($"connect failed: {ex.Message}");
        }
    }

    private async Task RunConfirmedAsync()
    {
        if (MessageBox.Show(this,
                "Write category-5 entitlement keys 1–25 to the current BO3 session?\n\nUse only with your offline dev-kit test profile.",
                "Confirm entitlement write", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        await RunAsync(Presets.Entitlements.Command);
    }

    private async Task RunAsync(string command)
    {
        if (!_client.Connected) { Log("not connected"); return; }
        try
        {
            Log("> " + command);
            foreach (string line in await _client.SendAsync(command)) Log("  " + line);
        }
        catch (Exception ex)
        {
            Log("  error: " + ex.Message);
            SetStatus("disconnected", false);
            _client.Close();
        }
    }

    private void SetStatus(string text, bool connected)
    {
        _status.Text = text;
        _status.ForeColor = connected ? Color.MediumSeaGreen : Color.IndianRed;
    }
}
