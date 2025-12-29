#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import socket

SERVER_HOST = "10.10.10.1"  # oder IP des Servers
SERVER_PORT = 56555        # Port des Servers
MESSAGE = "1|Hallo|Dies ist eine Testnachricht\n"  # ICON|TITLE|MESSAGE

def main():
    try:
        # Verbindung herstellen
        with socket.create_connection((SERVER_HOST, SERVER_PORT), timeout=5) as sock:
            print(f"Verbunden mit {SERVER_HOST}:{SERVER_PORT}")

            # Nachricht senden (UTF-8)
            sock.sendall(MESSAGE.encode("utf-8"))
            print(f"Gesendet: {MESSAGE.strip()}")

            # Antwort lesen (optional)
            response = sock.recv(1024).decode("utf-8", errors="replace")
            print(f"Antwort vom Server: {response.strip()}")
    except Exception as ex:
        print(f"Fehler: {ex}")

if __name__ == "__main__":
    main()
