# Enhanced Storage + Backpack

Singleplayer-Mod für Schedule I von codexgaming-de. Entwicklung mit KI-Unterstützung.

## Stand: 0.0.2 – Einstellungen testen

Registriert 22 MelonPreferences-Einstellungen in 11 Kategorien für Mod Manager &
Phone App: Sprache, Debug-Logging, Backpack-Slots und Hotkey sowie Slots und Reihen
für neun Lagertypen. Werteänderungen werden über MelonPreferences-Ereignisse sofort
im Log erfasst, sobald der Manager den Wert übernimmt. Kein Update-Polling.

**Storage- und Backpack-Spielmechanik ist noch nicht implementiert.** Diese Version
verändert keine Inventare, öffnet keinen Rucksack und speichert keine Gegenstände.
Die Sprachwahl ist vorbereitet; die Einstellungsbeschriftungen sind vorerst zweisprachig.
Kein Nexus-Release.

## Bauen auf Nobara

Voraussetzungen: .NET SDK 8 und Schedule I IL2CPP mit MelonLoader.
Spiele- und Loader-DLLs werden lokal referenziert und nicht mitgeliefert.

```bash
cd /home/codex/Enhanced-Storage-Backpack
git pull --ff-only origin main
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Nach erfolgreichem Build und bei beendetem Spiel:

```bash
cp "bin/Release/net6.0/EnhancedStorageBackpack.dll" \
   "/home/codex/Schreibtisch/Schedulue 1 Plugins/Mods/"
```

Für abweichende Referenzordner können `MelonLoaderDirectory` (MelonLoader/net6) und
`GameAssembliesDirectory` (MelonLoader/Il2CppAssemblies) einzeln übergeben werden.
Keine anderen MelonLoader-DLLs in den Mods-Ordner kopieren.

## Testablauf 0.0.2

1. Spiel starten: `ESB_SETTINGS_READY | 0.0.2` muss im Log erscheinen.
2. Im Hauptmenü im Mod Manager nach **Enhanced Storage + Backpack** filtern.
   Es müssen Allgemein, Rucksack und die neun Lagertypen angezeigt werden.
3. Dasselbe in einem Singleplayer-Spielstand in der Phone App prüfen.
4. Diagnose-Log einschalten, Rucksack-Slots von 40 auf 64 stellen, Hotkey auf N
   und Sprache auf English ändern. Small Storage Rack testweise auf 32 Slots und
   4 Reihen stellen. Änderungen gegebenenfalls mit Eingabe/Save im Manager übernehmen.
5. Ohne Spielneustart muss für jede tatsächliche Wertänderung eine passende
   `ESB_SETTING_CHANGED`-Zeile erscheinen. Das prüft die Einstellungen, noch keine
   Änderung am Lager oder Rucksack. Falls die Meldung erst nach Save erscheint,
   dies melden; die Übernahme hängt vom verwendeten Manager-Bedienelement ab.
6. Grenzen testen: Rucksack-Slots 0 werden auf 1 begrenzt, 129 auf 128.
   Storage-Slots -1 werden auf 0 begrenzt, 129 auf 128. Eine unveränderte effektive
   Einstellung löst keine neue Meldung aus. Ansicht erneut öffnen, falls die
   Eingabebox einen korrigierten Wert nicht direkt wiedergibt.
7. Einstellungen im Mod Manager speichern, Spiel neu starten und die Werte im
   Manager sowie anhand der `ESB_SETTING_LOADED`-Zeilen vergleichen.
8. Debug deaktivieren: weitere Änderungen dürfen das eigene Diagnose-Log nicht
   mehr erweitern. Alte Zeilen werden dabei nicht gelöscht.

Eigener Diagnosepfad: `UserData/EnhancedStorageBackpack-Debug.log`.
Bei 1 MiB wird die bisherige Datei als `.previous` aufbewahrt; höchstens ein Vorgänger.
Das normale MelonLoader-Log protokolliert in dieser Testversion Einstellungsänderungen
auch bei ausgeschaltetem Diagnose-Log. Keine zyklischen Logausgaben.

**Einstellungen speichern ist nicht Spielstand speichern.** Der spätere Rucksackinhalt
wird ausschließlich mit dem zugehörigen Spielstand gespeichert, niemals durch
Gegenstandsbewegungen oder die Save-Taste des Mod Managers.

## Vorläufige Vorgaben

- Backpack: 40 Slots, Hotkey B; zulässige Slotzahl 1–128.
- Lager: Slots/Rows jeweils 0 = Spielvorgabe; Slots bis 128.
- Reihen: vorläufig 0–128 als technische Eingabegrenze. Endgültige Layoutregeln
  folgen beim Storage-Schritt; diese Grenze ist kein bestätigtes UI-Layout.
- Sprache Deutsch; optionales Diagnose-Log aus.

## Validierung und Zusammenarbeit

0.0.1: Build lokal beim Nutzer erfolgreich und Laden durch das bereitgestellte Log
vom 25.09.2026 bestätigt (Schedule I 0.4.6f13, MelonLoader 0.7.3, Wine/Proton 11.0).
Mod Manager & Phone App 2.2.4 ist in diesem Log geladen.

0.0.2: Release-Build mit .NET SDK 8.0.425 gegen die bereitgestellten Referenzen
erfolgreich: 0 Warnungen, 0 Fehler. Der Test im Mod Manager
und in der Phone App steht aus. Ein erfolgreicher Build ersetzt keinen Laufzeittest.

Kleine Commits, Prüfung und gemeinsamer Test pro Schritt. Vor Änderungen Git-Status
prüfen und aktuellen Stand abrufen. Die Anforderungen stehen in [docs/ROADMAP.md](docs/ROADMAP.md).

Die Kategorien folgen dem Namensschema aus der [offiziellen Integrationsanleitung](https://github.com/Prowiler/schedule1-mod-manager-wiki/wiki/Integration-Guide).
Wir verwenden MelonLoaders eigene Änderungsereignisse, keine Mod-Manager-API-Aufrufe
und keine Referenz auf dessen DLL. Die Anzeige und Übernahme werden mit der
installierten Version gemeinsam geprüft.
