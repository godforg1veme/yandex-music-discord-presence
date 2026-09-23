using System.Drawing;
using System.Windows.Forms;

namespace YandexMusicDiscord;

internal static class Program
{
    // Public application ID shared by all installations; no user token is needed.
    private const string DefaultApplicationId = "1552113063606489088";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "YandexMusicDiscord", out var firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        var clientId = args.FirstOrDefault(arg => arg.StartsWith("--client-id=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
            ?? Environment.GetEnvironmentVariable("YANDEX_DISCORD_CLIENT_ID")
            ?? DefaultApplicationId;
        Application.Run(new TrayContext(clientId));
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ActivityCoordinator? _coordinator;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _tickRunning;

    public TrayContext(string clientId)
    {
        try { StartupRegistration.MigrateLegacyIfNeeded(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            // Startup migration is optional; the app can still run without it.
        }
        _statusItem = new ToolStripMenuItem("Запуск…") { Enabled = false };
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += ExitClicked;
        var startupItem = new ToolStripMenuItem("Запускать вместе с Windows") { Checked = StartupRegistration.IsEnabled() };
        startupItem.Click += (_, _) =>
        {
            try
            {
                StartupRegistration.SetEnabled(!startupItem.Checked);
                startupItem.Checked = StartupRegistration.IsEnabled();
            }
            catch (Exception ex)
            {
                _statusItem.Text = $"Ошибка автозапуска: {ex.Message}";
            }
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add(exitItem);
        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "Яндекс Музыка → Discord",
            ContextMenuStrip = menu,
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += TimerTick;
        if (string.IsNullOrWhiteSpace(clientId) || !clientId.All(char.IsDigit))
        {
            _statusItem.Text = "Укажите Discord Application ID";
            return;
        }

        _coordinator = new ActivityCoordinator(new WindowsMediaSource(), new TrackResolver(), new DiscordIpcClient(clientId));
        _timer.Start();
        _ = UpdateAsync();
    }

    private void TimerTick(object? sender, EventArgs e) => _ = UpdateAsync();

    private async Task UpdateAsync()
    {
        if (_tickRunning || _coordinator is null) return;
        _tickRunning = true;
        try
        {
            await _coordinator.TickAsync(_shutdown.Token);
            _statusItem.Text = _coordinator.Status.Length > 80 ? _coordinator.Status[..80] : _coordinator.Status;
        }
        finally { _tickRunning = false; }
    }

    private async void ExitClicked(object? sender, EventArgs e)
    {
        _timer.Stop();
        _shutdown.Cancel();
        if (_coordinator is not null) await _coordinator.StopAsync();
        _icon.Visible = false;
        _icon.Dispose();
        _timer.Dispose();
        _shutdown.Dispose();
        ExitThread();
    }
}
