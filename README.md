[TOC]

# Toaster

Eine Applikation, die sich mit einem "Message-Server" über TCP verbindet und Message, die der Server schickt, als "Toasts" (Popup) aufpoppen lässt.

Wurde entwickelt, um beim "blocker" (Internetsperre) entsprechende Vorwarnungen an den Client zu schicken, bevor das Inernet gesperrt wird.

## Konfiguration

Kommunikation mit dem Server:

Konfig-Variablen:

| Variable | Wert | Bedeutung |
 -|-|-|
| ServerHost | 10.10.10.1 | IP-Adresse oder Hostname des Servers |
| PortNumber | 56555 | TCP-Port, über den der Toaster sich mit dem Server verbindet |
    
## Usage

Einfach das Executable `Toaster.exe` starten. Die laufende Applikation wird als TrayIcon angezeigt. 

### Beenden

Über das Kontextmenü des TrayIcons.

## Message-Format

Die Messages, die im Toaster verarbeitet werden, haben das folgende Format:

* jede Message wird mit LF abgeschlossen
* eine Message besteht aus drei Teilen, separiert mit dem Pipe-Symbol:
    * Icon (1 == Info, 2 == Warning, 3 == Error)
    * Titel der Message
    * Message-Text

Beispiel:

```
msg = "1|Bildschirmzeit|Nur noch 2 Minuten verbleibend."
```


## Build

Pushen eines Git-Tags startet den CI/CD-Build. Das Ergebnis wird in der Github-Pipeline als Archiv abgelegt.

Die Versionsnummer wird automatisch aus dem Git-Tag erstellt. Dieser muss die Form `MAJOR.MINOR.PATCH haben`, also zB `1.1.3`. Der CI-Build wird dann so ausgelöst:

```
git tag 1.1.3
git push --tags
```

## Test-Software



### Testen mit Send.ps1

```
# Standard: Icon 1 (Info)
.\send.ps1 -Title "Ein Test" -Message "Hello from PowerShell! Do you see me? It's Alex!"

# Warning-Icon 2
.\send.ps1 -Icon 2 -Title "Achtung" -Message "Dies ist eine Warnung."

# Gegen einen anderen Host/Port
.\send.ps1 -Host 10.10.10.65 -Port 56555 -Title "Ping" -Message "Test 123"
```



## dotnet-Basics

### Projekt erstellen

```
dotnet new console -n TcpToastApp
cd TcpToastApp
```

#### NuGet-Paket installieren

```
dotnet add package CommunityToolkit.WinUI.Notifications
```

#### Code einfuegen

* Öffne Program.cs und ersetze den Inhalt mit dem Code, den ich dir gegeben habe.
* Ergänze am Anfang:

```
using Microsoft.Toolkit.Uwp.Notifications;
```

### App-ID registrieren

Damit Toasts funktionieren, fuege vor Main() ein:

```
ToastNotificationManagerCompat.RegisterApplicationId("TcpToastApp");
```

### Kompilieren und starten

```
dotnet build
dotnet build -c Release
dotnet run
```

Bzw. self-contained (eine einzige exe, keine .net-Runtime auf Zielsystem noetig):

```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### Build self-contained exe


```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
# EXE will be in: bin\Release\net8.0-windows\win-x64\publish\YourApp.exe
```

**Update:** die Settings wurden in `ToastApp.csproj` angepasst, sodass man diese Optionen nicht mehr extra angeben muss. Es reicht ein:

```
dotnet publish -c Release
```

... dann wird in bin\Release\net9.0-windows\win-x64\publish\ das Binary `ToastApp.exe` entstehen, das alle Libs inkludiert und als einfaches Exe weitergegeben werden kann.


## Wichtige Hinweise (obsolet?)

(Ich glaube, diese Hinweise sind veraltet und gelten nur für den anfänglichen Ansatz, dass "Toaster" als Server auf dem Windows-Host läuft. Inzwischen ist "Toaster" der Client, womit die App-ID-Registrierung nicht mehr notwendig ist.)

* Toasts funktionieren nur im Benutzerkontext (nicht als Service).
* App muss einmal gestartet sein, damit Windows die App-ID akzeptiert.
* Wenn Toasts nicht erscheinen: Prüfe Benachrichtigungseinstellungen in Windows.


### Use PID file (PID file for easy shutdown from scripts)

```
File.WriteAllText(Path.Combine(Path.GetTempPath(), "EckListener.pid"), Environment.ProcessId.ToString());
// Stop script:
// powershell: $pid = Get-Content "$env:TEMP\EckListener.pid"; Stop-Process -Id $pid -Force
```

## Sending version info

Having successfully established a connection to the server, the Toaster client would send as a first line the HELO line to introduce itself:

```
HELO from Toaster v1.2.1
```

Thus, you can determine the version info from connected clients on the server side.

## Menu items

In the Toaster app, you can use the following context menu items:

* 15 minutes
* 30 minutes
* 1 hour
* 90 minutes
* 2 hours
* Exit

The "minutes"/"hours" menu items would request access for the time specified. 

The option "Exit" exits the application.

## Set server host

The server to connect to, is hardcoded with `10.10.10.1`. However, you can override the server host by setting the environment variable `TOASTER_SERVER_HOST` to another IP address or host name.

E.g. in Powershell:

```powershell
$env:TOASTER_SERVER_HOST="127.0.0.1"
Toaster.exe
```
