using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms; // Für NotifyIcon
using System.Drawing;       // Für SystemIcons

class Program
{
    [STAThread] // Wichtig für Windows Forms
    static async Task Main(string[] args)
    {
        // Damit NotifyIcon funktioniert, brauchen wir eine MessageLoop
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        NotifyIcon trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information, // Jetzt funktioniert es
            Visible = true
        };

        int port = 5000;
        Console.WriteLine($"Listening on port {port}...");
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        // Async Task für Listener
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                using var reader = new StreamReader(client.GetStream());
                string message = await reader.ReadToEndAsync();
                Console.WriteLine($"Received: {message}");
                trayIcon.ShowBalloonTip(5000, "Neue Nachricht", message, ToolTipIcon.Info);
            }
        });

        // Startet die MessageLoop für das TrayIcon
        Application.Run();
    }
}