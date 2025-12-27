
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics; // für FileVersionInfo

#nullable enable

internal static class Program
{
    
    private static string GetAppVersion()
    {
        // robust: funktioniert für normale Builds und Single-File-Bundles
        string exePath = Environment.ProcessPath!;
        var fvi = FileVersionInfo.GetVersionInfo(exePath);

        // Bevorzugt: ProductVersion (aus AssemblyInformationalVersion),
        // Fallback: FileVersion
        string version = !string.IsNullOrWhiteSpace(fvi.ProductVersion)
            ? fvi.ProductVersion
            : (fvi.FileVersion ?? "unknown");

        return version;
    }

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // >>> Version beim Start ausgeben
        string version = GetAppVersion();
        Console.WriteLine($"Toaster v{version}");

        Application.Run(new TrayAppContext());
    }
}

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly CancellationTokenSource _cts = new();
    private readonly SynchronizationContext _uiContext;
    private Task? _listenerTask;

    public static int PortNumber { get; set; } = 56555;

    public TrayAppContext()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

       // Get current process executable path (works in single-file and normal)
        string exePath = Environment.ProcessPath!;

        // Extract the associated icon from the EXE (this reads the <ApplicationIcon>)
        using Icon associated = Icon.ExtractAssociatedIcon(exePath)!;

        // Scale to the system small icon size (tray prefers 16x16; handles high DPI properly)
        Icon trayIco = new Icon(associated, SystemInformation.SmallIconSize);

        // Create tray icon and context menu
        _trayIcon = new NotifyIcon
        {
            Icon = trayIco,
            Text = "Toaster",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, __) => ExitApplication());

        // // Optional: show a startup balloon (requires a message loop already running)
        // _trayIcon.ShowBalloonTip(2000, "Eck Listener", "Listening service starting…", ToolTipIcon.Info);

        // Start the listener (do not await)
        _listenerTask = StartTcpListenerAsync(_cts.Token);
        _listenerTask.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                // Report error to user via tray balloon (on UI thread)
                _uiContext.Post(_ =>
                    _trayIcon.ShowBalloonTip(5000, "Eck Listener Error",
                        t.Exception.GetBaseException().Message, ToolTipIcon.Error), null);
            }
        }, TaskScheduler.Default);
    }

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EckListener", "listener.log");

    private static void Log(string msg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
        Console.WriteLine(line);
        File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
    }



    private async Task StartTcpListenerAsync(CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Any, PortNumber);
        listener.Start();

        // // Optional: update tray that we’re listening
        // _uiContext.Post(_ => _trayIcon.ShowBalloonTip(2000, "Eck Listener",
        //     $"Listening on port {PortNumber}", ToolTipIcon.Info), null);

        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = HandleClientAsync(client, token); // fire-and-forget
            }
        }
        catch (OperationCanceledException)
        {
            // normal during shutdown
        }
        finally
        {
            try { listener.Stop(); } catch { /* ignore */ }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        // using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true })
        {
            // Notify connection on UI
            // _uiContext.Post(_ =>
            //     _trayIcon.ShowBalloonTip(1000, "Client connected",
            //         client.Client.RemoteEndPoint?.ToString() ?? "(unknown)", ToolTipIcon.Info), null);

            Log("Alex was here");

            string? line;
            while (!token.IsCancellationRequested &&
                (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                Log("irgendetwas empfangen ...");
                await writer.WriteLineAsync($"Echo: {line}").ConfigureAwait(false);

                Log(" line=" + line);
                var command = ParseInput(line);
                Log(" command=" + command);
                if (command.HasValue)
                {
                    var icon = command.Value.Icon switch
                    {
                        1 => ToolTipIcon.Info,
                        2 => ToolTipIcon.Warning,
                        3 => ToolTipIcon.Error,
                        _ => ToolTipIcon.None
                    };
                    Log("und zwar message=" + command.Value.Message);

                    _uiContext.Post(_ =>
                        _trayIcon.ShowBalloonTip(
                            5000,
                            command.Value.Title,
                            command.Value.Message,
                            icon), null);
                } else {
                    Log("nix gescheites empfangen ...");
                    _uiContext.Post(_ =>
                        _trayIcon.ShowBalloonTip(
                            1000,
                            "OJE",
                            "Oje, nix konnte gelesen werden ...",
                            ToolTipIcon.Error), null);

                }
            }

            // _uiContext.Post(_ =>
            //     _trayIcon.ShowBalloonTip(1000, "Client disconnected",
            //         client.Client.RemoteEndPoint?.ToString() ?? "(unknown)", ToolTipIcon.None), null);
        }

        // Avoid Thread.Sleep in async code
        // If you need pauses, use: await Task.Delay(5000, token);
    }

    public static (int Icon, string Title, string Message)? ParseInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var parts = input.Split('|');
        if (parts.Length != 3) {
            Log("error parsing reveiced message (no 3 pipe-separated text parts found), input=" + input);
            return null;
        }

        if (int.TryParse(parts[0], out int number) && number is >= 0 and <= 3)
        {
            return (number, parts[1], parts[2]);
        }
        return null;
    }

    private async void ExitApplication()
    {
        try
        {
            _cts.Cancel();

            if (_listenerTask is not null)
            {
                // give the listener a moment to shut down
                var delayTask = Task.Delay(1500);
                await Task.WhenAny(_listenerTask, delayTask);
            }
        }
        catch { /* ignore */ }
        finally
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            ExitThread(); // closes the ApplicationContext -> Application.Run returns
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}



