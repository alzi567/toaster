
param(
    [string] $Server = "10.10.10.65",
    [int]    $Port   = 56555,

    [ValidateSet(1,2,3)]
    [int]    $Icon = 1,

    [Parameter(Mandatory = $true)]
    [string] $Title,

    [Parameter(Mandatory = $true)]
    [string] $Message
)

# TCP-Verbindung herstellen
$client = [System.Net.Sockets.TcpClient]::new($Server, $Port)
try {
    $stream  = $client.GetStream()
    $writer  = [System.IO.StreamWriter]::new($stream, [System.Text.Encoding]::UTF8)
    $writer.AutoFlush = $true

    # Protokoll "ICON|TITLE|MESSAGE"
    $line = "{0}|{1}|{2}" -f $Icon, $Title, $Message

    # WriteLine fügt \r\n selbst an – kein \n am Ende nötig
    $writer.WriteLine($line)

    Write-Host ("Gesendet an {0}:{1} -> {2}" -f $Server, $Port, $line)
}
finally {
    if ($writer) { $writer.Dispose() }
    if ($stream) { $stream.Dispose() }
    $client.Close()
}
