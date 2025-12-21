using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

#nullable enable

class Program
{
    public static int PortNumber { get; set; } = 56555; // Global Port

    private static NotifyIcon trayIcon;
    private static SynchronizationContext? uiContext;


    // Ensure non-null static field
    static Program()
    {
        trayIcon = new NotifyIcon();
    }


    [STAThread]
    static async Task Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // UI-Thread SynchronizationContext speichern
        uiContext = SynchronizationContext.Current;

        // Tray-Icon erstellen
        trayIcon.Icon = SystemIcons.Information;
        trayIcon.Text = "Eck Listener";
        trayIcon.Visible = true;
        trayIcon.ContextMenuStrip = new ContextMenuStrip();
        trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) =>
        {
            trayIcon.Visible = false;
            Application.Exit();
        });


        trayIcon.ShowBalloonTip(1000, "Test", "Programmstart", ToolTipIcon.Info);
        // TCP-Listener starten (parallel)
        await Task.Run(() => StartTcpListenerAsync());

        // Message Loop starten
        Application.Run();
    }

    private static async Task StartTcpListenerAsync()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, PortNumber);
        listener.Start();
        Console.WriteLine($"Listening on port {PortNumber}...");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client); // Fire-and-forget
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        trayIcon.ShowBalloonTip(1000, "Test", "Ui Task-Start", ToolTipIcon.Info);
        Thread.Sleep(5000);
        trayIcon.ShowBalloonTip(1000, "Test2", "Noch ein Ui Task-Start", ToolTipIcon.Info);
        uiContext?.Post(_ =>
        {
            trayIcon.ShowBalloonTip(5000, "Test 3", "Der DRITTE Ui Task-Start", ToolTipIcon.Info);
        }, null);

        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"Received: {line}");
                await writer.WriteLineAsync($"Echo: {line}");

                var command = ParseInput(line);
                if (command.HasValue)
                {
                    ToolTipIcon icon = command.Value.Icon switch
                    {
                        1 => ToolTipIcon.Info,
                        2 => ToolTipIcon.Warning,
                        3 => ToolTipIcon.Error,
                        _ => ToolTipIcon.None
                    };

                    // UI-Aufruf sicher auf den UI-Thread marshallen
                    uiContext?.Post(_ =>
                    {
                        trayIcon.ShowBalloonTip(5000, command.Value.Title, command.Value.Message, icon);
                    }, null);
                }
                else
                {
                    Console.WriteLine("Invalid input format or number out of range.");
                }
            }
        }
        Console.WriteLine("Client disconnected.");
    }

    /// <summary>
    /// Parses an input string into three components: a number, a title, and a message.
    /// </summary>
    public static (int Icon, string Title, string Message)? ParseInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string[] parts = input.Split('|');
        if (parts.Length != 3)
            return null;

        if (int.TryParse(parts[0], out int number) && number >= 1 && number <= 3)
        {
            return (number, parts[1], parts[2]);
        }

        return null;
    }
}