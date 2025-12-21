# Toaster

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

Damit Toasts funktionieren, fuee vor Main() ein:

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

### Use PID file (PID file for easy shutdown from scripts)

```
File.WriteAllText(Path.Combine(Path.GetTempPath(), "EckListener.pid"), Environment.ProcessId.ToString());
// Stop script:
// powershell: $pid = Get-Content "$env:TEMP\EckListener.pid"; Stop-Process -Id $pid -Force
```