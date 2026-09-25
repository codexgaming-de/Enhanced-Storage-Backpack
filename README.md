# Enhanced Storage + Backpack

Neustart des Schedule-I-Mods von codexgaming-de. Entwicklung mit KI-Unterstuetzung.

## Stand: 0.0.1 – Ladetest

Diese Version schreibt beim Initialisieren genau eine Meldung:

```text
ESB_BOOTSTRAP_OK | 0.0.1 | Grundprojekt geladen.
```

Storage, Backpack, Hotkeys, Einstellungen und Mod-Manager-Anbindung werden erst
nach erfolgreichem Ladetest schrittweise implementiert. Diese Version besitzt
keine Spielstandlogik und keine Harmony-Patches. Kein Nexus-Release.

## Bauen auf Nobara

Voraussetzungen: .NET SDK 8 und eine Schedule-I-IL2CPP-Installation mit MelonLoader.
Der Spielpfad wird lokal uebergeben; Spiele- und Loader-DLLs gehoeren nicht ins Repository.

```bash
cd /home/codex/Enhanced-Storage-Backpack
git pull --ff-only origin main
dotnet build -c Release -p:GameDirectory="/vollstaendiger/Pfad/Schedule I"
```

Alternativ kann `-p:MelonLoaderDirectory="/Pfad/MelonLoader/net6"` angegeben werden.
Ausgabe: `bin/Release/net6.0/EnhancedStorageBackpack.dll`.

## Ladetest

1. Spiel beenden und alte Storage-/Backpack-Test-DLLs aus dem Mods-Ordner herausnehmen.
2. Die neu gebaute DLL in den Mods-Ordner kopieren.
3. Spiel starten und im MelonLoader-Log nach `ESB_BOOTSTRAP_OK` suchen.
4. Log bei Fehlern bereitstellen. Erst nach bestaetigtem Ladetest weiterentwickeln.

Ein erfolgreicher Build ersetzt keinen Test im Spiel. Der Ladetest auf Nobara/Proton
steht noch aus.

## Zusammenarbeit

Kleine Commits, nachvollziehbare Aenderungen, Build-Pruefung und gemeinsamer Test
im Spiel pro Entwicklungsschritt. Vor Aenderungen den Git-Status pruefen und den
aktuellen Stand abrufen. Bestehende lokale Aenderungen nicht ueberschreiben.
Die vereinbarten Storage- und Backpack-Funktionen bleiben das Entwicklungsziel.
Vor einer Nexus-Veroeffentlichung die dann geltenden KI-Kennzeichnungen pruefen.
