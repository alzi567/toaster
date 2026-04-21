#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Test-Skript für die neue "15 minutes" Feature.
Simuliert den Toaster-Client und sendet "cmd|allow|15m" an den Server.
"""

import socket
import sys
import time


def main():
    HOST = "127.0.0.1"
    PORT = 56555

    print(f"[*] Verbinde zu {HOST}:{PORT}...")
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.connect((HOST, PORT))
            print("[+] Verbunden!")

            # Schritt 1: HELO senden (wie Toaster es macht)
            helo = "HELO from Toaster v1.0 (test)\n"
            sock.sendall(helo.encode("utf-8"))
            print(f"[>] Gesendet: {helo.strip()}")
            time.sleep(0.5)

            # Schritt 2: cmd|allow|15m senden (simuliert "15 minutes" Menu-Klick)
            cmd = "cmd|allow|15m\n"
            sock.sendall(cmd.encode("utf-8"))
            print(f"[>] Gesendet: {cmd.strip()}")
            print("[+] Test erfolgreich! Der Server sollte 'cmd|allow|15m' empfangen haben.")
            time.sleep(1)

    except ConnectionRefusedError:
        print(f"[-] Fehler: Konnte nicht zu {HOST}:{PORT} verbinden. Server läuft nicht?")
        sys.exit(1)
    except Exception as e:
        print(f"[-] Fehler: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
