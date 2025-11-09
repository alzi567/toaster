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
dotnet run
```



### Testen

```
echo "Hallo Welt" | nc 127.0.0.1 5000
```

## Wichtige Hinweise

* Toasts funktionieren nur im Benutzerkontext (nicht als Service).
* App muss einmal gestartet sein, damit Windows die App-ID akzeptiert.
* Wenn Toasts nicht erscheinen: Prüfe Benachrichtigungseinstellungen in Windows.
