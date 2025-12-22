
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import socket
import click

def recv_line(sock: socket.socket, max_bytes: int = 65536) -> str:
    """
    Liest bis zum ersten '\n' (inklusive) und gibt die Zeile ohne CRLF zurück.
    Begrenzung über max_bytes gegen unendliche Streams.
    """
    chunks = bytearray()
    while True:
        if len(chunks) >= max_bytes:
            raise click.ClickException("Antwort zu lang oder kein Zeilenende gefunden.")

        try:
            b = sock.recv(1)
        except socket.timeout as ex:
            raise click.ClickException(f"Timeout beim Warten auf die Antwort: {ex}") from ex

        if not b:
            # Server hat Verbindung geschlossen, bevor '\n' kam
            break

        chunks += b
        if b == b'\n':
            break

    # CRLF/ LF normalisieren
    text = chunks.decode("utf-8", errors="replace")
    if text.endswith("\r\n"):
        text = text[:-2]
    elif text.endswith("\n"):
        text = text[:-1]
    return text

def send_and_expect(
    server: str,
    port: int,
    icon: int,
    title: str,
    message: str,
    timeout: float | None,
    expected: str | None
) -> bool:
    """
    Stellt Verbindung her, sendet 'icon|title|message' (UTF-8, CRLF),
    liest eine Antwortzeile und prüft auf Gleichheit mit 'expected'
    (oder dem gesendeten Payload, falls expected None).
    """
    if icon not in (1, 2, 3):
        raise click.BadParameter("Icon muss 1, 2 oder 3 sein.")

    payload = f"{icon}|{title}|{message}"
    data = (payload + "\r\n").encode("utf-8")

    with socket.create_connection((server, port), timeout=timeout) as sock:
        if timeout is not None:
            sock.settimeout(timeout)

        # Senden (robust gegen Teilsendungen)
        total_sent = 0
        while total_sent < len(data):
            sent = sock.send(data[total_sent:])
            if sent == 0:
                raise click.ClickException("Socket-Verbindung unterbrochen während des Sendens.")
            total_sent += sent

        # Antwort lesen (eine Zeile)
        response = recv_line(sock)

    # Erwartung festlegen
    expected_text = payload if expected is None else expected

    ok = (response == expected_text)
    if ok:
        click.echo(f"OK: Echo stimmt überein.\n  gesendet : {payload}\n  empfangen: {response}")
    else:
        click.echo(f"FEHLER: Echo weicht ab.\n  gesendet : {payload}\n  empfangen: {response}\n  erwartet : {expected_text}", err=True)
    return ok

@click.command(context_settings=dict(help_option_names=['-h', '--help']))
@click.option('--server', '-s', default='10.10.10.65', show_default=True,
              help='Ziel-Host/IP.')
@click.option('--port', '-p', default=56555, show_default=True, type=int,
              help='Ziel-Port.')
@click.option('--icon', '-i', default=1, show_default=True,
              type=click.Choice(['1', '2', '3'], case_sensitive=False),
              help='Icon-Code: 1=Info, 2=Warning, 3=Error.')
@click.option('--title', '-t', required=True, help='Titel-Text.')
@click.option('--message', '-m', required=True, help='Nachrichten-Text.')
@click.option('--timeout', default=5.0, show_default=True, type=float,
              help='Verbindungs-/Antwort-Timeout (Sekunden).')
@click.option('--expect', default=None,
              help='Erwartete Antwort für die Echo-Prüfung. '
                   'Standard: exakt der gesendete Payload "icon|title|message".')
def cli(server: str, port: int, icon: str, title: str, message: str, timeout: float, expect: str | None):
    """
    Sende 'ICON|TITLE|MESSAGE' (UTF-8, CRLF) an den Listener, warte auf eine Antwortzeile
    und prüfe, ob die Rückgabe dem Erwartungswert entspricht (standardmäßig identisch zum Payload).
    """
    try:
        ok = send_and_expect(
            server=server,
            port=port,
            icon=int(icon),
            title=title,
            message=message,
            timeout=timeout,
            expected=expect
        )
        # Exitcode 0 bei Erfolg, 1 bei Abweichung
        raise SystemExit(0 if ok else 1)
    except click.ClickException:
        # Bereits formatiert; einfach weiterreichen.
        raise
    except Exception as ex:
        # Unerwartete Fehler sauber melden
        raise click.ClickException(str(ex)) from ex

if __name__ == '__main__':
    cli()
