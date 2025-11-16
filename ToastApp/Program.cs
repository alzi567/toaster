using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

class Program
{
    /// <summary>
    /// Global port number for the application.
    /// </summary>
    public static int PortNumber { get; set; } = 56555; // Default value

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

        int port = PortNumber;
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
                    var result = ParseInput(message);

                    Console.WriteLine($"Received: {message}");
                    if (result.HasValue)
                    {
                        Console.WriteLine($"- Icon: {result.Value.Icon}");
                        Console.WriteLine($"- Title: {result.Value.Title}");
                        Console.WriteLine($"- Message: {result.Value.Message}");

                        // Map integer to ToolTipIcon
                        ToolTipIcon icon = result.Value.Icon switch
                        {
                            1 => ToolTipIcon.Info,
                            2 => ToolTipIcon.Warning,
                            3 => ToolTipIcon.Error,
                            _ => ToolTipIcon.None // fallback for invalid values
                        };

                        trayIcon.ShowBalloonTip(5000, result.Value.Title, result.Value.Message, icon);
                    }
                    else
                    {
                        Console.WriteLine("Invalid input format or number out of range.");
                    }
                }
            }
        });

        Application.Run();
    }

    /// <summary>
    /// Parses an input string into three components: a number, a title, and a message.
    /// </summary>
    /// <param name="input">
    /// The input string in the format: "number|title|message".
    /// </param>
    /// <returns>
    /// A tuple (Number, Title, Message) if parsing succeeds and the number is in the range 1–3;
    /// otherwise, returns <c>null</c>.
    /// </returns>
    /// <remarks>
    /// - The input must contain exactly three parts separated by a pipe ('|').
    /// - The first part must be an integer between 1 and 3.
    /// </remarks>
    public static (int Icon, string Title, string Message)? ParseInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string[] parts = input.Split('|');

        if (parts.Length != 3)
            return null;

        if (int.TryParse(parts[0], out int number) && number >= 1 && number <= 3)
        {
            string title = parts[1];
            string message = parts[2];
            return (number, title, message);
        }

        return null; // Invalid number or format
    }

}