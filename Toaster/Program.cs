
using System;
using System.IO;
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
    private Task? _clientTask;

    // ==== Client-Konfiguration ====
    public static string ServerHost { get; set; } = "10.10.10.1";
    public static int PortNumber { get; set; } = 56555;
    public static int ReconnectDelayMs { get; set; } = 2000;
    public static bool SendAck { get; set; } = false;
    public static string AckText { get; set; } = "OK";

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
            Text = "Toaster", // optional: $"Toaster (Client) {ServerHost}:{PortNumber}"
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, __) => ExitApplication());

        // Optionaler Start-Hinweis
        // _trayIcon.ShowBalloonTip(2000, "Toaster Client", $"Verbinde zu {ServerHost}:{PortNumber}…", ToolTipIcon.Info);

        // Starte den TCP-Client-Loop (nicht awaiten)
        _clientTask = StartTcpClientLoopAsync(_cts.Token);
        _clientTask.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                // Fehler als Tray-Balloon melden (auf UI-Thread)
                Log($"Toaster Client Fehler: {t.Exception.GetBaseException().Message}");
                _uiContext.Post(_ =>
                    _trayIcon.ShowBalloonTip(5000, "Toaster Client Fehler",
                        t.Exception.GetBaseException().Message, ToolTipIcon.Error), null);
            }
        }, TaskScheduler.Default);
    }

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Toaster", "listener.log");

    private static void Log(string msg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
        Console.WriteLine(line);
        File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
    }

    // ==== NEU: Client-Schleife ====
    private async Task StartTcpClientLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                Log($"Verbinde zu {ServerHost}:{PortNumber} …");
                client = new TcpClient();
                // Hinweis: In .NET 6 gibt es kein ConnectAsync mit CancellationToken für TcpClient.
                await client.ConnectAsync(ServerHost, PortNumber).ConfigureAwait(false);
                Log("Verbunden.");

                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true })
                {
                    // Optional: kleinen Hinweis im Tray
                    // _uiContext.Post(_ =>
                    //     _trayIcon.ShowBalloonTip(1500, "Verbunden",
                    //         $"{ServerHost}:{PortNumber}", ToolTipIcon.Info), null);

                    string? line;
                    while (!token.IsCancellationRequested &&
                           (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        Log($"Empfangen: {line}");

                        var command = ParseInput(line);
                        if (command.HasValue)
                        {
                            var icon = command.Value.Icon switch
                            {
                                1 => ToolTipIcon.Info,
                                2 => ToolTipIcon.Warning,
                                3 => ToolTipIcon.Error,
                                _ => ToolTipIcon.None
                            };

                            _uiContext.Post(_ =>
                                _trayIcon.ShowBalloonTip(
                                    5000,
                                    command.Value.Title,
                                    command.Value.Message,
                                    icon), null);

                            if (SendAck)
                            {
                                try
                                {
                                    await writer.WriteLineAsync(AckText).ConfigureAwait(false);
                                    Log($"ACK gesendet: {AckText}");
                                }
                                catch (Exception ex)
                                {
                                    Log($"Fehler beim ACK-Senden: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Log("Ungültige Zeile – kein 3-teiliger Pipe-String.");
                            _uiContext.Post(_ =>
                                _trayIcon.ShowBalloonTip(
                                    1500, "Ungültige Nachricht",
                                    "Erwartet: ICON|TITLE|MESSAGE",
                                    ToolTipIcon.Warning), null);
                        }
                    }

                    Log("Server hat die Verbindung geschlossen.");
                    // _uiContext.Post(_ =>
                    //     _trayIcon.ShowBalloonTip(1500, "Getrennt",
                    //         "Server hat beendet oder Verbindung verloren.", ToolTipIcon.None), null);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal während Shutdown
                break;
            }
            catch (Exception ex)
            {
                Log($"Verbindungs-/Lese-Fehler: {ex.Message}");
                // _uiContext.Post(_ =>
                //     _trayIcon.ShowBalloonTip(3000, "Verbindungsfehler",
                //         ex.Message, ToolTipIcon.Error), null);
            }
            finally
            {
                try { client?.Close(); } catch { /* ignore */ }
            }

            // Reconnect-Delay
            if (!token.IsCancellationRequested)
            {
                Log($"Erneuter Verbindungsversuch in {ReconnectDelayMs} ms …");
                try
                {
                    await Task.Delay(ReconnectDelayMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        Log("Client-Schleife beendet.");
    }

    public static (int Icon, string Title, string Message)? ParseInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var parts = input.Split('|');
        if (parts.Length != 3) {
            Log("error parsing received message (no 3 pipe-separated text parts found), input=" + input);
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
        Log("Toaster wird beendet.");
        try
        {
            _cts.Cancel();

            if (_clientTask is not null)
            {
                // der Schleife kurz Zeit zum sauberen Ende geben
                var delayTask = Task.Delay(1500);
                await Task.WhenAny(_clientTask, delayTask);
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
