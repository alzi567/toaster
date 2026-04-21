#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Test-Skript für die erweiterte Zeit-basierte Allow-Commands.
Simuliert alle 5 Menu-Klicks: 15m, 30m, 1h, 90m, 2h
"""

import socket
import sys
import time


def test_command(host: str, port: int, command: str, delay: float = 0.5) -> bool:
    """Sende einen Command an den Server"""
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.connect((host, port))
            
            # HELO senden
            helo = f"HELO from Toaster test (testing {command})\n"
            sock.sendall(helo.encode("utf-8"))
            print(f"  [>] HELO gesendet")
            time.sleep(0.2)
            
            # Command senden
            cmd_line = command + "\n"
            sock.sendall(cmd_line.encode("utf-8"))
            print(f"  [>] Command gesendet: {command}")
            time.sleep(delay)
            
            return True
    except Exception as e:
        print(f"  [-] Fehler: {e}")
        return False


def main():
    HOST = "127.0.0.1"
    PORT = 56555
    
    commands = [
        "cmd|allow|15m",
        "cmd|allow|30m",
        "cmd|allow|1h",
        "cmd|allow|90m",
        "cmd|allow|2h"
    ]
    
    print(f"[*] Test-Skript für erweiterte Time-Allow Commands")
    print(f"[*] Ziel: {HOST}:{PORT}\n")
    
    success_count = 0
    for i, command in enumerate(commands, 1):
        print(f"[{i}/{len(commands)}] Teste: {command}")
        if test_command(HOST, PORT, command):
            success_count += 1
        print()
    
    print(f"\n[+] Test abgeschlossen: {success_count}/{len(commands)} erfolgreich")
    
    if success_count == len(commands):
        print("[+] Alle Commands wurden erfolgreich gesendet!")
        return 0
    else:
        print("[-] Einige Commands konnten nicht gesendet werden.")
        return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n[!] Abgebrochen.")
        sys.exit(1)
