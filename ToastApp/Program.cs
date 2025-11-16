using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

#nullable enable

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

        int port = PortNumber;
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Listening on port {port}...");

        _ = Task.Run(async () =>
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client); // Fire-and-forget
            }
        });

        Application.Run();
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })

        {
            NotifyIcon trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true
            };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"Received: {line}");
                await writer.WriteLineAsync($"Echo: {line}"); // Optional response

                var command = ParseInput(line);
                if (command.HasValue)
                {
                    Console.WriteLine($"- Icon: {command.Value.Icon}");
                    Console.WriteLine($"- Title: {command.Value.Title}");
                    Console.WriteLine($"- Message: {command.Value.Message}");

                    // Map integer to ToolTipIcon
                    ToolTipIcon icon = command.Value.Icon switch
                    {
                        1 => ToolTipIcon.Info,
                        2 => ToolTipIcon.Warning,
                        3 => ToolTipIcon.Error,
                        _ => ToolTipIcon.None // fallback for invalid values
                    };

                    trayIcon.ShowBalloonTip(5000, command.Value.Title, command.Value.Message, icon);
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
