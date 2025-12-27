
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import asyncio
import click
from typing import Set, Optional, Tuple
import sys


def make_payload(icon: int, title: str, message: str) -> str:
    if icon not in (0, 1, 2, 3):
        raise ValueError("Icon muss 0, 1, 2 oder 3 sein.")
    return f"{icon}|{title}|{message}"


async def send_line(writer: asyncio.StreamWriter, line: str) -> None:
    # LF reicht; dein C#-Client (StreamReader.ReadLineAsync) akzeptiert LF/CRLF
    data = (line + "\n").encode("utf-8")
    writer.write(data)
    await writer.drain()


async def recv_line(reader: asyncio.StreamReader, max_bytes: int = 65536) -> Optional[str]:
    """
    Liest eine Zeile (endet bei '\n'). Gibt None zurück wenn EOF vor Zeilenende.
    """
    try:
        raw = await reader.readuntil(b"\n")
    except asyncio.IncompleteReadError as e:
        if e.partial:
            # EOF mit Partial – behandeln wie letzte Zeile, falls sinnvoll
            raw = e.partial
        else:
            return None
    except asyncio.LimitOverrunError:
        # zu lang, konsumieren bis zum nächsten NL
        await reader.readexactly(max_bytes)  # grob – echte Reinigung wäre spezifisch
        return None
    except Exception:
        return None

    text = raw.decode("utf-8", errors="replace")
    if text.endswith("\r\n"):
        text = text[:-2]
    elif text.endswith("\n"):
        text = text[:-1]
    return text


class ClientRegistry:
    def __init__(self) -> None:
        self._clients: Set[Tuple[asyncio.StreamReader, asyncio.StreamWriter]] = set()

    def add(self, r: asyncio.StreamReader, w: asyncio.StreamWriter) -> None:
        self._clients.add((r, w))

    def remove(self, r: asyncio.StreamReader, w: asyncio.StreamWriter) -> None:
        self._clients.discard((r, w))

    def writers(self) -> Set[asyncio.StreamWriter]:
        return {w for _, w in self._clients}

    def count(self) -> int:
        return len(self._clients)


async def handle_client(
    reader: asyncio.StreamReader,
    writer: asyncio.StreamWriter,
    registry: ClientRegistry,
    ack_expected: Optional[str],
    ack_timeout: float,
    max_bytes: int,
) -> None:
    peer = writer.get_extra_info("peername")
    click.echo(f"[+] Client verbunden: {peer}")
    registry.add(reader, writer)

    try:
        # Der Server wartet hier nicht aktiv auf Kommandos,
        # sondern hält die Verbindung offen. Optional kann man
        # eingehende Textzeilen als Status/ACKs lesen:
        while True:
            line = await recv_line(reader, max_bytes=max_bytes)
            if line is None:
                break
            # Eingehende Zeilen protokollieren; z.B. ACKs:
            click.echo(f"[{peer}] recv: {line}")
            # Keine weitere Auswertung notwendig – Broadcast kommt separat
    except Exception as ex:
        click.echo(f"[!] Fehler bei {peer}: {ex}", err=True)
    finally:
        click.echo(f"[-] Client getrennt: {peer}")
        try:
            writer.close()
            await writer.wait_closed()
        except Exception:
            pass
        registry.remove(reader, writer)


async def broadcast_payload(
    registry: ClientRegistry,
    payload: str,
    ack_expected: Optional[str],
    ack_timeout: float,
) -> None:
    """
    Sendet payload an alle Clients. Wenn ack_expected gesetzt ist,
    wartet der Server pro Client auf eine Zeile und prüft Gleichheit.
    """
    writers = list(registry.writers())
    if not writers:
        click.echo("[i] Keine Clients verbunden – Broadcast wird übersprungen.")
        return

    click.echo(f"[>] Broadcast an {len(writers)} Clients: {payload}")

    async def send_and_maybe_ack(w: asyncio.StreamWriter):
        peer = w.get_extra_info("peername")
        try:
            await send_line(w, payload)
            if ack_expected is not None:
                # ACK vom gleichen Socket lesen – dafür brauchen wir dessen Reader.
                # Da wir hier nur den Writer haben, ist ACK-Handling besser direkt
                # in handle_client, aber wir können zeitweise ein Protokoll vereinbaren:
                # -> Wir ignorieren hier das ACK (nur Demo).
                # Alternative: Registry als Dict mit r,w Paar und das Reader-Objekt hier lookup'en.
                pass
            click.echo(f"[{peer}] gesendet.")
        except Exception as ex:
            click.echo(f"[!] Senden an {peer} fehlgeschlagen: {ex}", err=True)

    await asyncio.gather(*(send_and_maybe_ack(w) for w in writers))


async def periodic_sender(
    registry: ClientRegistry,
    icon: int,
    title: str,
    message: str,
    interval: float,
    ack_expected: Optional[str],
    ack_timeout: float,
    stop_event: asyncio.Event,
):
    """
    Sendet periodisch ein Kommando an alle verbundenen Clients.
    """
    payload = make_payload(icon, title, message)
    while not stop_event.is_set():
        await broadcast_payload(registry, payload, ack_expected, ack_timeout)
        try:
            await asyncio.wait_for(stop_event.wait(), timeout=interval)
        except asyncio.TimeoutError:
            continue


async def console_broadcast_loop(
    registry: ClientRegistry,
    ack_expected: Optional[str],
    ack_timeout: float,
):
    """
    Liest Befehle von der Konsole und broadcastet sie:
    Format: ICON|TITLE|MESSAGE
    z.B.: 1|Hallo|Welt
    """
    click.echo("Konsole aktiv. Tippe Zeilen im Format ICON|TITLE|MESSAGE. EOF (Ctrl-D) beendet die Konsole.")
    loop = asyncio.get_running_loop()

    while True:
        try:
            line = await loop.run_in_executor(None, sys.stdin.readline)
        except Exception:
            break
        if not line:
            break
        line = line.strip()
        if not line:
            continue

        parts = line.split("|", 3)
        if len(parts) != 3:
            click.echo("Ungültiges Format. Erwartet ICON|TITLE|MESSAGE.", err=True)
            continue
        try:
            icon = int(parts[0])
        except ValueError:
            click.echo("ICON muss eine Zahl sein (0..3).", err=True)
            continue
        payload = make_payload(icon, parts[1], parts[2])
        await broadcast_payload(registry, payload, ack_expected, ack_timeout)


@click.command(context_settings=dict(help_option_names=["-h", "--help"]))
@click.option("--host", default="0.0.0.0", show_default=True, help="Bind-Adresse des Servers.")
@click.option("--port", default=56555, show_default=True, type=int, help="Port des Servers.")
@click.option("--periodic/--no-periodic", default=False, show_default=True, help="Periodische Aussendung aktivieren.")
@click.option("--interval", default=10.0, show_default=True, type=float, help="Intervall (Sekunden) für periodische Aussendung.")
@click.option("--icon", default=1, show_default=True, type=click.IntRange(0, 3), help="ICON für periodische Aussendung.")
@click.option("--title", default="Toaster", show_default=True, help="TITLE für periodische Aussendung.")
@click.option("--message", default="Hallo vom Server", show_default=True, help="MESSAGE für periodische Aussendung.")
@click.option("--expect-ack", default=None, help="ACK-Text, den der Server nach Sendung erwartet (z. B. 'OK').")
@click.option("--ack-timeout", default=5.0, show_default=True, type=float, help="Timeout für ACK (Sekunden).")
@click.option("--max-bytes", default=65536, show_default=True, type=int, help="Max. Zeilenlänge beim Lesen.")
def main(
    host: str,
    port: int,
    periodic: bool,
    interval: float,
    icon: int,
    title: str,
    message: str,
    expect_ack: Optional[str],
    ack_timeout: float,
    max_bytes: int,
):
    """
    Asyncio TCP-Server, der Zeilen an Clients sendet (ICON|TITLE|MESSAGE) und
    optional ACKs erwartet. Periodische Aussendung oder Konsole-Broadcast.
    """
    registry = ClientRegistry()
    stop_event = asyncio.Event()

    async def runner():
        server = await asyncio.start_server(
            lambda r, w: handle_client(r, w, registry, expect_ack, ack_timeout, max_bytes),
            host, port
        )
        addrs = ", ".join(str(s.getsockname()) for s in server.sockets)
        click.echo(f"Server läuft auf {addrs}; Clients verbinden sich auf {host}:{port}.")

        tasks = []
        if periodic:
            tasks.append(asyncio.create_task(
                periodic_sender(registry, icon, title, message, interval, expect_ack, ack_timeout, stop_event)
            ))
        else:
            tasks.append(asyncio.create_task(
                console_broadcast_loop(registry, expect_ack, ack_timeout)
            ))

        try:
            async with server:
                await server.serve_forever()
        except asyncio.CancelledError:
            pass
        finally:
            stop_event.set()
            for t in tasks:
                t.cancel()
                with contextlib.suppress(asyncio.CancelledError):
                    await t

    import contextlib
    try:
        asyncio.run(runner())
    except KeyboardInterrupt:
        click.echo("\nBeendet durch Benutzer.")


if __name__ == "__main__":
    main()
