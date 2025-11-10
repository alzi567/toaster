using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        NotifyIcon trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true
        };

        int port = 5000;
        Console.WriteLine($"Listening on port {port}...");
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                using var reader = new StreamReader(client.GetStream());
                string message;
                while ((message = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine($"Received: {message}");
                    trayIcon.ShowBalloonTip(5000, "Neue Nachricht", message, ToolTipIcon.Info);
                }
            }
        });

        Application.Run();
    }
}