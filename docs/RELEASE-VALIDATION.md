# Release-Prüfung 0.1.4

Stand: 27.09.2026. Ausgangspunkt: Commit
`7a781729fd6c7a0e32d1528c6f3356696dce173b` (0.1.3),
Branch `feature/0.1.0-backpack`.

**Status: Blocked für die endgültige Veröffentlichung.** Spielprüfungen und ein
reguläres net6.0-Release-Artefakt für 0.1.4 fehlen noch. Die Vorbereitung ist
abgeschlossen, es wurde nichts auf Nexus veröffentlicht.

## Änderung 0.1.4

In Mod.cs nutzten InventorySaving und InventoryLoading dieselbe Active-Abfrage
wie interaktive Updates. Ein Fehler in Run oder OnUpdate setzt disabled=true.
Danach übersprang der Save-Hook den Backpack ohne Fehlermeldung, obwohl dessen
Zustand noch vorhanden war. Ein späterer Save konnte damit den Zusatz weglassen.
Dies ist ein durch den Kontrollfluss belegter Fehler, kein berichteter Datenverlust.

Die Persistenz-Hooks prüfen jetzt ihre eigene PersistenceActive-Bedingung, die
nicht vom interaktiven disabled-Flag abhängt. Die bisherigen Prüfungen für lokale
Spieler und Client-only sowie Fehlerbehandlung und Speicherformat bleiben erhalten.
Bei einem Fehler beim Erstellen des Snapshots wird weiterhin ReportSaveError
aufgerufen und die Ausnahme weitergegeben. Bei Ladefehlern wird weiterhin abgebrochen.
Es werden keine neuen Dateien geschrieben und keine Gegenstände geklont.

Die Initialisierungsfehler-Behandlung (UnpatchSelf) ist davon nicht erfasst.
Eine fehlgeschlagene Initialisierung ist kein sicher nutzbarer Mod-Betrieb.
Eine Garantie für die Fehlerbehandlung des nativen Spiel-Savers folgt daraus nicht.

## Evidenz

| Bereich | Ergebnis | Grenze |
| --- | --- | --- |
| Neun Lagertypen, getrennte Größen, Verkleinern und Umräumen | Nutzer bestätigt | Schrittweise Tests bis 0.0.13 |
| Lager Speichern/Laden | Nutzer bestätigt | Keine simulierten Schreibabbrüche |
| DE/EN im Mod Manager | Nutzer bestätigt | Manager 2.2.4 |
| Backpack, Seiten, Größenänderungen, Speichern/Laden | Nutzer bestätigt | Keine eigene Spielausführung hier |
| Unsaved Hotbar/Backpack-Bewegungen zurücksetzen | Nutzer bestätigt über Backpack-Testplan | Kein erzwungener Prozessabbruch |
| Layout, letzte Lagerseite, entfernter Backpack-Untertitel | Nutzer bestätigt für 0.1.3 | 0.1.4 ändert kein Layout |
| Größen- und Seitenregeln | 32.779 Prüfungen bestanden | Reine C#-Regeln |
| Backpack-JSON und Snapshot-Verhalten | 268 Prüfungen bestanden | Kein nativer Spiel-Saver |
| Ein Log, letzte drei Sitzungen, Umschalten und Migration | Tests bestanden | Isolierte Dateisystemtests |
| Kompilierung 0.1.4 | Direkte Roslyn-Prüfung bestanden | 12 CS1701-Referenzwarnungen, net8-Prüfumgebung |
| Regulärer net6.0-Build 0.1.4 | Noch offen | MSBuild-Werkzeugfehler dieser Umgebung |
| Persistenz nach interner Abschaltung | Codeprüfung bestanden; Laufzeit offen | Neuer Fehlerfallschutz in 0.1.4 |
| Polizeidurchsuchung | Noch offen | Eigener Slot-Owner allein beweist natives Verhalten nicht |
| Spielstand A → B → A; unterbrochener Save | Noch offen | Codec-Tests ersetzen diese Spieltests nicht |
| Performance | Nicht gemessen | Ereignisbasierte Größenanpassung; keine FPS-Zusage |
| Multiplayer | Nicht anwendbar | Ausdrücklich außerhalb des Projekts |

Compiler: SDK 8.0.425, direkte csc-Prüfung gegen bereitgestellte IL2CPP- und
MelonLoader-Referenzen. Reguläres MSBuild scheitert hier bereits bei
System.Diagnostics.Process.GetStat/GetStartTime. Das direkte Prüfergebnis ist
**keine zur Installation bestimmte net6.0-DLL**.

Ausgeführte Befehle dieser Prüfungsumgebung:

```bash
tooling/dotnet/dotnet tooling/dotnet/sdk/8.0.425/Roslyn/bincore/csc.dll @inspection/check.rsp
tooling/dotnet/dotnet tooling/dotnet/sdk/8.0.425/Roslyn/bincore/csc.dll @inspection/rulestests.rsp
tooling/dotnet/dotnet inspection/RulesTests.dll
tooling/dotnet/dotnet tooling/dotnet/sdk/8.0.425/Roslyn/bincore/csc.dll @inspection/backpacktests.rsp
tooling/dotnet/dotnet inspection/BackpackTests.dll
tooling/dotnet/dotnet tooling/dotnet/sdk/8.0.425/Roslyn/bincore/csc.dll @inspection/logtests.rsp
tooling/dotnet/dotnet inspection/LogTests.dll
```

Die rsp-Dateien sind lokale Prüfhilfen mit lokalen Referenzpfaden, keine
Repository-Abhängigkeit. Reproduzierbare reguläre Projektbefehle stehen im README.

## Nächster Spieltest

Mit einer Kopie des Spielstands und aktiviertem eigenem Debug-Log:

1. 0.1.4 regulär bauen und installieren. Im Log muss ESB_READY 0.1.4 stehen.
2. Illegalen Gegenstand mit bekannter Menge im Backpack ablegen, auch auf einer
   späteren Seite. Separat einen im normalen Inventar ablegen, den die Polizei
   normalerweise erkennt. Mengen notieren, dann regulär speichern.
3. Polizeidurchsuchung vollständig durchführen. Normaler Inventargegenstand muss
   gemäß Vanilla behandelt werden; Backpack-Gegenstand muss unverändert bleiben.
   Ist die Kontrolle nicht wirklich durchgeführt worden, ist der Test ungültig.
4. Backpack wieder öffnen: Item entnehmbar, Menge korrekt, kein eingefrorenes Menü.
   Anschließend speichern, neu laden und erneut Inhalt/Mengen prüfen.
5. Spielstand A und B mit eindeutig unterschiedlichen Backpack-Inhalten speichern.
   A → B → A laden; Inhalte dürfen nicht vermischt werden.
6. Speichern/Laden sowie beide Bewegungsrichtungen ohne zwischenzeitlichen
   Spiel-Save erneut kurz prüfen. Auf Spiel-Autosaves achten: sie zählen als Save.

Fehlerfallprüfungen (interne Abschaltung, Schreibfehler/Abbruch) bleiben getrennte
Entwicklungstests mit isolierten Saves. Keine Originalspielstände beschädigen und
keine künstlichen Fehler in der öffentlichen DLL belassen.

Ergebnisse und das eigene Debug-Log ergänzen, danach finalen net6.0-Build,
Versionsnummer und Paketinhalt prüfen. In das Paket gehören Mod-DLL und eigene
Dokumentation; keine Spiel-DLLs, Loader-DLLs, Spielstände, Einstellungen oder Logs.
