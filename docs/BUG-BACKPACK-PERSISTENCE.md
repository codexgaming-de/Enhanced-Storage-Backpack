# Backpack-Inhalt fehlt nach Speichern und Neustart

27.09.2026, Ausgangsversion 0.1.4, Commit
52df5bad3d65e71870c1abd7d91a9edd73b2eb61.
Schweregrad: Inhaltsverlust; Veröffentlichung blockiert.

## Beobachtung

Nutzer: Gegenstände ablegen, Spiel speichern, beenden, starten/laden → Rucksack leer.
Polizeidurchsuchung ignoriert den Rucksack wie gewünscht (Nutzerbestätigung).

Log Enhanced-Storage-Backpack-Debug(20260927-103912).log:
- Sitzung 12:33:25: ESB_READY 0.1.4.
- 12:33:48 und 12:37:49 jeweils zwei LOAD_PAYLOAD mit present=False.
- Anschließend READY slots=128, occupied=0.
- Kein ESB_BACKPACK_SERIALIZE im gesamten gelieferten Log.
- Keine ESB_BACKPACK_SAVE-, LOAD- oder DISABLED-Fehlermeldung.

Das belegt fehlenden Payload am beobachteten Ladeeingang. Es belegt nicht,
welcher native Aufruf beim Speichern tatsächlich ausgeführt wurde, und auch
nicht, dass keine Spielstand-Sicherung mehr Inhalte enthält.

## Hypothesen

1. GetInventoryString-Hook wird im verwendeten Save-Pfad nicht erreicht.
   Gestützt durch fehlende Serialisierungseinträge. Noch unbestätigt.
2. Hook wird erreicht, aber LocalPlayer/PersistenceActive überspringt ihn.
   Derselbe Logbefund; durch Eintrag vor der Prüfung unterscheidbar.
3. Hook wird erreicht, Zustand aber noch nicht bereit und ohne Payload.
   Gegenindiz: vorher READY und OPEN. Durch Zustandsdiagnose unterscheidbar.
4. Payload wird später entfernt oder überschrieben. Erst weiter untersuchen,
   wenn tatsächliche Serialisierung beobachtet wurde.

Vorliegende IL2CPP-DLL liefert native Methodensignaturen und Aufrufwrapper,
keine vollständigen nativen Methodenimplementierungen. Ein Spiel-Save und
repräsentative Inventory-Datei stehen dieser Prüfung nicht zur Verfügung.

## 0.1.5: gezielte Diagnose, keine behauptete Reparatur

Neue optionale ESB_SAVE_PATH-Einträge an:
- SaveManager.Save (beide vorhandenen Signaturen),
- PlayerManager.WriteData (Eintritt/Austritt),
- PlayerManager.SavePlayer,
- Player.WriteData (Eintritt/Austritt),
- Player.GetInventoryString (vor unserer Spielerprüfung),
- Player.LoadInventory (vor unserer Spielerprüfung).

Erfasst werden lokale Spielerzuordnung, Client-only-/Abschaltzustand sowie
Backpack-Bereitschaft, Slotzahl und belegte Plätze. Keine Spielerkennungen,
Dateipfade oder vollständigen Inventarinhalte. Kein periodisches Logging,
keine zusätzlichen Saves, keine Änderungen am Speicherformat und keine
separate Backpack-Datei. Diagnosefehler dürfen den Spiel-Save nicht abbrechen.
Die einzelne Debug-Datei und ihr Drei-Sitzungen-Verlauf bleiben bestehen.

## Prüfung und nächster Schritt

Direkte Roslyn-Compilerprüfung: bestanden, zwölf bekannte CS1701-Warnungen
in der lokalen net8-Prüfumgebung. Kein regulärer net6.0-Build oder Spieltest hier.
Codec-Tests ersetzen diesen fehlenden Integrationstest ausdrücklich nicht.

Auf einer Spielstandkopie Debug-Logging einschalten, 0.1.5 laden, einen
entbehrlichen Gegenstand in den Backpack legen, regulär speichern und auf das
Ende des Speichervorgangs warten. Spiel vollständig schließen, neu starten und
denselben Stand laden. Dann das eigene Debug-Log bereitstellen.

Anhand der Save-Grenzen die erste Abweichung bestimmen und erst dann den
betroffenen Hook/Filter korrigieren. Danach denselben Neustartfall sowie beide
Bewegungsrichtungen ohne Save und Spielstandwechsel prüfen. Keine automatische
Speicherung bei Bewegungen als Ersatz einführen.
