> Aktuell 0.1.5: Backpack-Inhaltsverlust nach Neustart gemeldet. Fehlerbehebung
> hat Vorrang vor Release-Arbeiten. Polizeidurchsuchung vom Nutzer bestätigt.
> Frühere Speicherbestätigungen gelten nicht als abschließende Prüfung.
> Siehe [Fehleruntersuchung](BUG-BACKPACK-PERSISTENCE.md).

# Projektplan

Singleplayer-Mod Enhanced Storage + Backpack für Schedule I IL2CPP.

## Vom Nutzer im Spiel bestätigt

- Alle neun Lagertypen mit getrennten Slot-/Reihenwerten bis 128 Slots.
- Live-Einstellungen, Verkleinern mit Umräumen und Erhalt benötigter Zusatzplätze.
- Gegenstandsdarstellung in zusätzlichen Regalplätzen, soweit Stellfläche reicht.
- Sprachwechsel Deutsch/English im Mod Manager & Phone App.
- Backpack mit nativem Menü, Hotkey, 1–128 Slots und Seitennavigation.
- Speichern/Laden; Rückkehr zum gespeicherten Inhalt nach ungespeicherten Bewegungen.
- Layout bis 0.1.3, einschließlich kompakter letzter Lagerseite.

## Speichervertrag

Bewegungen verändern den laufenden Zustand. Erst reguläres Speichern des Spiels
(einschließlich Spiel-Autosave) hält ihn fest. Kein eigener Inhaltssave bei
Mod-Manager-Speichern, Größenänderung oder Menüschließen. Backpack und Hotbar
werden im selben Inventar-JSON gespeichert. Codec-Prüfungen ersetzen keine
Spieltests bei Schreibfehlern oder Abbrüchen des gesamten Speichervorgangs.

## Aktuell: Release-Vorbereitung 0.1.4

- Schutz gegen übersprungene Backpack-Persistenz bei interner Laufzeitabschaltung.
- Automatische Prüfungen und aktuelle Dokumentation.
- Offen: Polizeidurchsuchung, expliziter Spielstandwechsel, Fehlerfall-Speicherung,
  regulärer net6.0-Build und Spielprüfung des neuen Schutzes.
- Danach Paket und Nexus-Veröffentlichung mit korrekter KI-Kennzeichnung.

Siehe [Prüfbericht](RELEASE-VALIDATION.md) und [Nexus-Entwurf](NEXUS-RELEASE.md).
Frühere versionsbezogene Dokumente bleiben als historische Entwicklungsnotizen;
für den aktuellen Status ist der Prüfbericht maßgeblich.

## Grenzen

Kein Multiplayer. Keine gemessene Performance-Zusage. Ereignisbasierte
Größenänderungen, wiederverwendete UI und optionales begrenztes Logging.
Maximal 40 Plätze pro Seite, maximal 5 sichtbare Lagerreihen und 10 Spalten.
Eine dauerhafte Debug-Datei mit höchstens drei protokollierten Spielsitzungen.
