# Backpack 0.1.0 – erster Spieltest

## Umsetzung

- Eigener IL2CPP-IItemSlotOwner, ohne zusätzliche StorageEntity oder NetworkObject.
- 1–128 Slots, Standard 40; 40 pro Seite, eigene < / > Navigation.
- Natives StorageMenu und native ItemSlotUI; Quick Move auf die sichtbare Seite.
- Hotkey aus MelonPreferences (Standard B), gelesen über Unity Input System.
- DE/EN, sichere Größenänderung mit Umräumen bestehender Slots.
- Keine Registrierung im PlayerInventory: Polizeiinventar bleibt getrennt.
  Das tatsächliche Durchsuchungsverhalten ist noch im Spiel zu prüfen.
- Ereignisgesteuerte Größenänderung; keine Welt-Suche oder Dateioperation pro Frame.

## Speichervertrag

Player.GetInventoryString wird um das JSON-Feld EnhancedStorageBackpack ergänzt.
Es enthält Version=1 und Items (native ItemData-JSON-Strings oder null).
Hotbar und Rucksack werden dadurch als ein gemeinsamer Inventar-String an den
nativen Speichervorgang übergeben. Das Plugin schreibt selbst keine Save-Dateien.
Itembewegung, Einstellungsänderung, Menüschließen und Beenden lösen kein eigenes
Speichern aus. Normale Spiel-Autosaves gelten ebenfalls als Spielstandspeicherung.

Player.LoadInventory entfernt das Zusatzfeld vor dem nativen Laden; nach Ende
des Ladevorgangs werden Itemtypen über die nativen ItemLoader wiederhergestellt.
Fehlendes Feld bedeutet leerer Rucksack. Jeder StartGame-Aufruf verwirft den alten
Sitzungsinhalt. Ein unbekanntes Format oder nicht ladbares Item sperrt den Rucksack;
der ursprüngliche Payload wird für spätere Saves erhalten, nicht geleert.

Es ist im Spiel zu bestätigen, dass die native Pipeline GetInventoryString bis
zur Datei unverändert übernimmt und LoadInventory mit diesem Datensatz aufruft.
Der Code kann nicht die Crash-Konsistenz der gesamten nativen Spielspeicherung
verbessern. Speichern ohne aktiviertes Plugin erhält dessen Zusatzfeld nicht
notwendigerweise; für den Entwicklungstest bleibt das Plugin installiert.

## Lokale Validierung

- Direkte Roslyn-Kompilierung aller Mod-Dateien mit Spiel-Interop-Assemblies:
  erfolgreich, elf CS1701-Warnungen durch .NET-8- statt .NET-6-Referenzen.
- 268 reine Persistenzprüfungen: 1–128 Plätze, Item-Metadaten, unveränderter
  Vanilla-Inventarinhalt, fehlender Payload, getrennte Snapshots, ungültige Daten
  und unbekannte Versionsdaten.
- Diese Prüfungen bestätigen weder IL2CPP-Interface-Injection noch native
  Speicherung oder die Benutzeroberfläche zur Laufzeit. Regulärer .NET-6-Build
  und Spieltest stehen aus.

## Gezielter Spieltest

Für diesen ersten Persistenztest eine Spielstandkopie verwenden.

1. Laden, B drücken, wieder schließen. Keine Verzögerung/Blockade; Bewegungs-
   und Mauseingabe nach dem Schließen wieder normal. ESC/Schließen ebenfalls testen.
2. Gegenstand hineinlegen/entnehmen, Quick Move und Teilstapel testen.
3. Größe 81: Seiten 1–3, Gegenstand in Slot 81. Auf 10 verkleinern bei freien
   vorderen Plätzen; Gegenstand muss erreichbar sein. Überfüllung schützt Inhalt.
4. DE/EN sowie einen anderen Hotkey prüfen. Anschließend ein Lagerregal öffnen:
   keine Backpack-Navigation, keine falschen Slots oder Bindungen.
5. Speichern mit Item in Hotbar und leerem Backpack. Item in Backpack verschieben,
   ohne weiteren manuellen oder automatischen Save neu laden. Item nur in Hotbar.
6. Item in Backpack verschieben, speichern, neu laden: nur im Backpack vorhanden.
7. Aus gespeichertem Backpack in Hotbar verschieben und ohne Save neu laden:
   Item nur im Backpack. Mengen und Metadaten bleiben gleich.
8. Anderen/neuen Spielstand laden: kein Inhalt aus dem vorherigen Spielstand.
9. Polizeidurchsuchung: Backpack-Inhalt darf nicht durchsucht/entfernt werden.
10. Eigenes Debug-Log senden: LOAD_PAYLOAD, READY, SERIALIZE und mögliche Fehler.

Für eine Rückkehr zur geprüften Storage-Version kann der bestehende Branch
feature/0.0.7-medium-rack (0.0.13) gebaut werden. Zuvor Backpack leeren und mit
aktivierter Backpack-Version speichern, falls dessen Items erhalten bleiben sollen.


## Nachtrag 0.1.1

Nutzer meldet alle angeforderten Tests bestanden. Screenshot image(10).png zeigt
128 Plätze: fünf Slotreihen laufen nach unten, Navigation überlappt die Hotbar,
Fertig liegt in der zweiten Slotreihe. Ursache im Code: Slotraster erweitert,
aber native Titel-/Schließen-Positionen nicht an die Rasterhöhe angepasst.

Gemeinsames PagedMenuChrome ordnet die Elemente anhand der tatsächlichen
Canvas-Bounds nach dem normalen Unity-Layoutpass an; keine globalen erzwungenen
Canvas-Neuberechnungen. Beim Schließen werden ursprüngliche lokale Positionen
wiederhergestellt. Alle drei Regaltypen verwenden dieselbe Navigation/Layoutlogik.

Prüfen: Backpack 128 (Seiten 1–4), Fertig unter Navigation und Abstand zur Hotbar;
Regale 128 mit 1/3/5 Reihen, letzte Seite und Slot 128; von letzter Seite auf
10 verkleinern; wiederholt zwischen Backpack, Regal und Schrank wechseln.
Visuelle Bestätigung dieser Anpassung steht aus.
