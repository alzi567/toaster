# Toaster

## Build

Pushen eines Git-Tags startet den CI/CD-Build. Das Ergebnis wird in der Github-Pipeline als Archiv abgelegt.

Die Versionsnummer wird automatisch aus dem Git-Tag erstellt. Dieser muss die Form `MAJOR.MINOR.PATCH haben`, also zB `1.1.3`. Der CI-Build wird dann so ausgelöst:

```
git tag 1.1.3
git push --tags
```

## Projekt erstellen

```
dotnet new console -n TcpToastApp
cd TcpToastApp
```

### NuGet-Paket installieren

```
dotnet add package CommunityToolkit.WinUI.Notifications
```

### Code einfuegen

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



### Testen

```
echo "Hallo Welt" | nc 127.0.0.1 5000
```

## Wichtige Hinweise

* Toasts funktionieren nur im Benutzerkontext (nicht als Service).
* App muss einmal gestartet sein, damit Windows die App-ID akzeptiert.
* Wenn Toasts nicht erscheinen: Prüfe Benachrichtigungseinstellungen in Windows.


## Port

Der Port, auf dem Message empfangen werden, ist 56555 (hartkodiert).

## Message senden

Eine Message ist so definiert:

* jede Zeile stellt eine Message dar (abgeschlossen mit Zeilenende)
* drei Teile haben, separiert mit dem Pipe-Symbol:
    * Icon (1 == Info, 2 == Warning, 3 == Error)
    * Titel der Message
    * Message-Text

Beispiel:

```
msg = "1|Bildschirmzeit|Nur noch 2 Minuten verbleibend."
```


### Build self-contained exe


```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
# EXE will be in: bin\Release\net8.0-windows\win-x64\publish\YourApp.exe
```

**Update:** die Settings wurden in `ToastApp.csproj` angepasst, sodass man diese Optionen nicht mehr extra angegben muss. Es reicht ein:

```
dotnet publish -c Release
```

... dann wird in bin\Release\net9.0-windows\win-x64\publish\ das Binary `ToastApp.exe` entstehen, das alle Libs inkludiert und als einfaches Exe weitergegeben werden kann.

### Use PID file (PID file for easy shutdown from scripts)

```
File.WriteAllText(Path.Combine(Path.GetTempPath(), "EckListener.pid"), Environment.ProcessId.ToString());
// Stop script:
// powershell: $pid = Get-Content "$env:TEMP\EckListener.pid"; Stop-Process -Id $pid -Force
```

### Testen mit Send.ps1

```
# Standard: Icon 1 (Info)
.\send.ps1 -Title "Ein Test" -Message "Hello from PowerShell! Do you see me? It's Alex!"

# Warning-Icon 2
.\send.ps1 -Icon 2 -Title "Achtung" -Message "Dies ist eine Warnung."

# Gegen einen anderen Host/Port
.\send.ps1 -Host 10.10.10.65 -Port 56555 -Title "Ping" -Message "Test 123"
```

