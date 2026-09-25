# Vereinbarter Funktionsumfang

Die folgende Liste beschreibt Ziele, keine bereits implementierten Funktionen.
Aktueller Schritt: 0.0.1 kompilieren und Laden mit MelonLoader im Spiel bestaetigen.

## Backpack

- 1 bis 128 konfigurierbare Slots.
- Integration in das native StorageMenu.
- 40 Slots pro Seite mit eigener Navigation `<` und `>`.
- Konfigurierbarer Hotkey, Vorgabe B.
- Deutsch und Englisch.
- MelonPreferences, kompatibel mit Mod Manager & Phone App.
- Sicheres Verkleinern: belegte Slots nicht abschneiden.
- Polizeidurchsuchungen ignorieren den Rucksackinhalt.
- Optionales Debug-Logging mit eigenem Diagnose-Log.

## Storage

Slots und Reihen pro Lagertyp separat konfigurierbar:

- Small / Medium / Large Storage Rack.
- Small / Medium / Large / Huge Storage Closet.
- Safe.
- Filing Cabinet.

Alle Einstellungen ueber den Mod Manager im Spiel; Aenderungen sofort ohne
Spielneustart wirksam. Belegte Slots bei Groessenaenderungen schuetzen.

## Entwicklung

Neuer Code im Repository `codexgaming-de/Enhanced-Storage-Backpack`.
Lokaler Ordner des Nutzers: `/home/codex/Enhanced-Storage-Backpack/`.
GitHub-Zugriff auf dem Nutzer-PC per vorhandenem SSH-Schluessel.
Keine automatische Uebernahme des alten Implementierungsstands.
Nach jedem abgegrenzten Schritt Build pruefen und gemeinsam im Spiel testen.
Multiplayer-Verhalten vor dessen Implementierung konkret abstimmen.
