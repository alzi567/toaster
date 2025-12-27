
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import socket
import click


def make_payload(icon: int, title: str, message: str) -> str:
    if icon not in (0, 1, 2, 3):
        raise click.BadParameter("Icon muss 0, 1, 2 oder 3 sein.")
    return f"{icon}|{title}|{message}"


@click.command(context_settings=dict(help_option_names=["-h", "--help"]))
@click.option("--host", default="0.0.0.0", show_default=True, help="Bind-Adresse.")
@click.option("--port", default=56555, show_default=True, type=int, help="Port.")
def main(host: str, port: int):
    """
    Einfacher TCP-Server (blocking), akzeptiert einen Client und sendet
    interaktiv Zeilen im Format ICON|TITLE|MESSAGE.
    """
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as srv:
        srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        srv.bind((host, port))
        srv.listen(1)
        click.echo(f"Server wartet auf {host}:{port} …")

        conn, addr = srv.accept()
        click.echo(f"Client verbunden: {addr}")
        with conn:
            while True:
                try:
                    line = input("ICON|TITLE|MESSAGE > ").strip()
                except EOFError:
                    break
                if not line:
                    continue
                parts = line.split("|", 3)
                if len(parts) != 3:
                    click.echo("Ungültiges Format.")
                    continue
                try:
                    icon = int(parts[0])
                except ValueError:
                    click.echo("ICON muss Zahl sein.")
                    continue
                payload = make_payload(icon, parts[1], parts[2])
                data = (payload + "\n").encode("utf-8")
                conn.sendall(data)
    click.echo("Beendet.")


if __name__ == "__main__":
    main()
