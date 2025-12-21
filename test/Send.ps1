
param(
    [string] $Host = "10.10.10.65",
    [int]    $Port = 56555,

    [ValidateSet(1,2,3)]
    [int]    $Icon = 1,

    [Parameter(Mandatory=$true)]
    [string] $Title,

    [Parameter(Mandatory=$true)]
    [string] $Message
)

# TCP-Verbindung herstellen
$client = [System.Net.Sockets.TcpClient]::new($Host, $Port)
try {
    $stream = $client.GetStream()

    # UTF-8 StreamWriter mit AutoFlush
    $encoding = [System.Text.Encoding]::UTF8
    $writer   = [System.IO.StreamWriter]::new($stream, $encoding)
    $writer.AutoFlush = $true

    # Protokoll: "ICON|TITLE|MESSAGE"
    $line = "{0}|{1}|{2}" -f $Icon, $Title, $Message

    # Senden (WriteLine hängt \r\n automatisch an)
    $writer.WriteLine($line)

    # Optional: eine Bestätigungszeile ausgeben
    Write-Host ("Gesendet an {0}:{1} -> {2}" -f $Host, $Port, $line)
}
finally {
    # Aufräumen
    if ($writer) { $writer.Dispose() }
    if ($stream) { $stream.Dispose() }
    $client.Close()
}
