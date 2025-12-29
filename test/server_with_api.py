
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import asyncio
import sys
from typing import Optional, Tuple, Set, Dict, List

import click
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field, conint
import uvicorn


# --------------- Protokoll-Helfer ---------------

def make_payload(icon: int, title: str, message: str) -> str:
    if icon not in (0, 1, 2, 3):
        raise ValueError("Icon muss 0, 1, 2 oder 3 sein.")
    # Eine Zeile, LF am Ende beim Senden
    return f"{icon}|{title}|{message}"


async def send_line(writer: asyncio.StreamWriter, line: str) -> None:
    data = (line + "\n").encode("utf-8")
    writer.write(data)
    await writer.drain()


async def recv_line(reader: asyncio.StreamReader, max_bytes: int = 65536) -> Optional[str]:
    """
    Liest eine Zeile bis '\n'. Gibt None zurück, wenn EOF ohne Zeilenende.
    """
    try:
        raw = await reader.readuntil(b"\n")
    except asyncio.IncompleteReadError as e:
        if not e.partial:
            return None
        raw = e.partial
    except asyncio.LimitOverrunError:
        # Zeile zu lang – grob ausräumen:
        await reader.readexactly(max_bytes)
        return None
    except Exception:
        return None

    text = raw.decode("utf-8", errors="replace")
    if text.endswith("\r\n"):
        text = text[:-2]
    elif text.endswith("\n"):
        text = text[:-1]
    return text


# --------------- Client-Registry ---------------

class ClientRegistry:
    """
    Hält verbundene Clients (Reader/Writer) und erlaubt Broadcasts.
    """
    def __init__(self) -> None:
        # wir halten eine Menge aus (reader, writer)
        self._clients: Set[Tuple[asyncio.StreamReader, asyncio.StreamWriter]] = set()

    def add(self, r: asyncio.StreamReader, w: asyncio.StreamWriter) -> None:
        self._clients.add((r, w))

    def remove(self, r: asyncio.StreamReader, w: asyncio.StreamWriter) -> None:
        self._clients.discard((r, w))

    def peers(self) -> List[str]:
        out: List[str] = []
        for _, w in self._clients:
            try:
                out.append(str(w.get_extra_info("peername")))
            except Exception:
                out.append("<unknown>")
        return out

    def writers(self) -> List[asyncio.StreamWriter]:
        return [w for _, w in self._clients]

    def __len__(self) -> int:
        return len(self._clients)


registry = ClientRegistry()


# --------------- TCP-Server-Handler ---------------

async def handle_client(
    reader: asyncio.StreamReader,
    writer: asyncio.StreamWriter,
    max_bytes: int
) -> None:
    peer = writer.get_extra_info("peername")
    print(f"[+] Client verbunden: {peer}")
    registry.add(reader, writer)

    try:
        # Wir halten die Verbindung offen und lesen ggf. Rückmeldungen/ACKs.
        while True:
            line = await recv_line(reader, max_bytes=max_bytes)
            if line is None:
                break
            print(f"[{peer}] recv: {line}")
    except Exception as ex:
        print(f"[!] Fehler bei {peer}: {ex}", file=sys.stderr)
    finally:
        print(f"[-] Client getrennt: {peer}")
        try:
            writer.close()
            await writer.wait_closed()
        except Exception:
            pass
        registry.remove(reader, writer)


async def broadcast_payload(payload: str) -> Dict[str, int]:
    """
    Sendet payload an alle Clients. Entkoppelt Fehler pro Client.
    Gibt {attempted, ok, failed} zurück.
    """
    writers = registry.writers()
    attempted = len(writers)
    ok = 0
    for w in list(writers):
        try:
            await send_line(w, payload)
            ok += 1
        except Exception as ex:
            peer = w.get_extra_info("peername")
            print(f"[!] Senden an {peer} fehlgeschlagen: {ex}", file=sys.stderr)
            # Writer schließen und entfernen
            try:
                w.close()
                await w.wait_closed()
            except Exception:
                pass
            # Passenden Reader finden und entfernen
            for r, ww in list(registry._clients):
                if ww is w:
                    registry.remove(r, ww)
                    break
    return {"attempted": attempted, "ok": ok, "failed": attempted - ok}


# --------------- FastAPI-App ---------------

app = FastAPI(title="Toaster TCP Server API", version="1.0")

class MessageIn(BaseModel):
    icon: conint(ge=0, le=3) = Field(..., description="0=None, 1=Info, 2=Warning, 3=Error")
    title: str
    message: str

class SendResult(BaseModel):
    attempted: int
    ok: int
    failed: int

@app.get("/health")
async def health():
    return {"status": "ok"}

@app.get("/clients")
async def clients():
    return {"count": len(registry), "peers": registry.peers()}

@app.post("/send", response_model=SendResult)
async def send(msg: MessageIn):
    payload = make_payload(msg.icon, msg.title, msg.message)
    result = await broadcast_payload(payload)
    if result["attempted"] == 0:
        # Kein Fehler, aber Hinweis, dass niemand verbunden ist
        # HTTP 200 mit Result ist ok – oder optional 409 zurückgeben.
        pass
    return result


# --------------- Uvicorn-Server im selben Loop starten ---------------

async def serve_api(host: str, port: int):
    config = uvicorn.Config(app, host=host, port=port, loop="asyncio", lifespan="on", log_level="info")
    server = uvicorn.Server(config)
    # Diese awaitet, bis der Server gestoppt wird (Ctrl+C oder .should_exit = True)
    await server.serve()


# --------------- Optionale Konsole zum Broadcast ---------------

async def console_broadcast_loop():
    """
    Lies Zeilen von stdin und sende sie als 'ICON|TITLE|MESSAGE'
    """
    loop = asyncio.get_running_loop()
    print("Konsole aktiv. Tippe 'ICON|TITLE|MESSAGE' (z. B. 1|Titel|Nachricht). Ctrl-D/Strg-Z beendet.")
    while True:
        line = await loop.run_in_executor(None, sys.stdin.readline)
        if not line:
            break
        line = line.strip()
        if not line:
            continue
        parts = line.split("|", 2)
        if len(parts) != 3:
            print("Ungültiges Format. Erwartet ICON|TITLE|MESSAGE.", file=sys.stderr)
            continue
        try:
            icon = int(parts[0])
            payload = make_payload(icon, parts[1], parts[2])
        except Exception as ex:
            print(f"Fehler: {ex}", file=sys.stderr)
            continue
        result = await broadcast_payload(payload)
        print(f"Gesendet: {result['ok']}/{result['attempted']} (fehlgeschlagen: {result['failed']})")


# --------------- Main / CLI ---------------

@click.command(context_settings=dict(help_option_names=["-h", "--help"]))
@click.option("--host", default="0.0.0.0", show_default=True, help="Bind-Adresse für TCP-Server.")
@click.option("--port", default=56555, show_default=True, type=int, help="Port für TCP-Server.")
@click.option("--api-host", default="127.0.0.1", show_default=True, help="Bind-Adresse für die API.")
@click.option("--api-port", default=8000, show_default=True, type=int, help="Port für die API.")
@click.option("--max-bytes", default=65536, show_default=True, type=int, help="Maximale Zeilenlänge beim Lesen.")
@click.option("--console/--no-console", default=False, show_default=True, help="Interaktiven Konsolen-Broadcast starten.")
def main(host: str, port: int, api_host: str, api_port: int, max_bytes: int, console: bool):
    """
    Asyncio TCP-Server + FastAPI-API in einem Prozess.
    """
    async def runner():
        server = await asyncio.start_server(lambda r, w: handle_client(r, w, max_bytes), host, port)
        addrs = ", ".join(str(s.getsockname()) for s in server.sockets)
        print(f"TCP-Server läuft auf {addrs}. API unter http://{api_host}:{api_port}")

        tasks = [
            asyncio.create_task(server.serve_forever(), name="tcp-serve"),
            asyncio.create_task(serve_api(api_host, api_port), name="api-serve"),
        ]
        if console:
            tasks.append(asyncio.create_task(console_broadcast_loop(), name="console"))

        try:
            # Warte, bis eine der Aufgaben endet (normalerweise nie, außer Fehler oder Stop)
            done, pending = await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
            # Wenn etwas unerwartet endet, cancel den Rest
            for t in pending:
                t.cancel()
                with contextlib.suppress(asyncio.CancelledError):
                    await t
            # Wenn es hierher kommt, gab es wahrscheinlich einen Fehler in 'done'
            for t in done:
                exc = t.exception()
                if exc:
                    raise exc
        finally:
            server.close()
            await server.wait_closed()

    import contextlib
    try:
        asyncio.run(runner())
    except KeyboardInterrupt:
        print("\nBeendet durch Benutzer.")

if __name__ == "__main__":
    main()
