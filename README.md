> **Development branch — 0.2.0-dev.4. Not a playable multiplayer beta.**
> SteamNetworkLib 1.6.0 IL2CPP, a bidirectional version handshake and session-only host settings are implemented.
> Install the dependency separately in `UserLibs/SteamNetworkLib.dll` before building/running this development branch.
> Client inventory access remains blocked pending synchronization and persistence.
> Stable singleplayer release: 0.1.6 on `main`.
> See [multiplayer development and testing](docs/MULTIPLAYER-DEVELOPMENT.md).

# Enhanced Storage + Backpack

Singleplayer-Mod für **Schedule I (IL2CPP)** von **CoDeX-Gaming**.
Mit Codex/KI entwickelter Code; Anforderungen und Spieltests durch den Projektbetreiber.

**Stand: 0.1.6, normale Spieltests bestanden; Release-Paket in Vorbereitung.**
Bestätigt sind Speichern/Laden über Hauptmenü und vollständigen Neustart,
ungespeicherte Bewegungen ohne Duplikate, getrennte Spielstände A → B → A
und Polizeidurchsuchungen. Zusätzlich auf dem Hauptspielstand getestet.
Die Prüfung des tatsächlich verpackten Builds steht noch aus.

## Funktionen

- Backpack: 1–128 Plätze, standardmäßig 40; konfigurierbare Taste (Standard **B**).
- Natives StorageMenu mit eigener `<` / `>` Navigation, bis zu 40 Plätze pro Seite.
- Neun Lagertypen mit getrennten Einstellungen für Plätze und Reihen:
  Small/Medium/Large Storage Rack, Small/Medium/Large/Huge Storage Closet,
  Safe und Filing Cabinet.
- Lagerplätze: 1–128; **0** verwendet die ursprüngliche Spielvorgabe.
- Lagerseiten: maximal 40 Plätze, 5 sichtbare Reihen und 10 Spalten.
  1–3 Reihen ergeben Seiten mit höchstens 10/20/30 Plätzen. Höhere
  Reiheneinstellungen bleiben gespeichert, die sichtbare Seite nutzt höchstens 5.
  Die letzte Seite benötigt nur so viele Reihen wie ihre verbleibenden Plätze.
- Beim Verkleinern ziehen Gegenstände in geeignete freie vordere Plätze um.
  Können sie nicht untergebracht werden, bleiben die benötigten Zusatzplätze
  erhalten. Gegenstände werden nicht einfach abgeschnitten.
- Deutsch/English für unsere Einstellungen, Beschreibungen und Menütitel.
  Fremde Schaltflächen des Mod Managers behalten dessen eigene Sprache.
- MelonPreferences; Einstellungen im Mod Manager & Phone App veränderbar.
  Änderungen wirken während der Sitzung; während Ziehen, Speichern oder Laden
  werden Größenänderungen zurückgestellt. Nur DLL-Updates brauchen einen Neustart.
- Optional eine Debug-Datei mit den letzten drei protokollierten Spielsitzungen.

Die sichtbare Gegenstandsdarstellung eines Regals ist durch dessen Stellfläche
begrenzt; 128 Plätze bedeuten nicht 128 gleichzeitig sichtbare Gegenstandsmodelle.
Multiplayer und die Mono-Version des Spiels werden nicht unterstützt.

## Speichern und Laden

Lager verwenden die Speicherverwaltung des Spiels. Der Backpack wird in denselben
Inventar-JSON-Datensatz eingebettet wie das Spielerinventar. Gegenstandsbewegungen,
Menüschließen und Mod-Manager-Einstellungen lösen keinen eigenen Inhaltssave aus.

Nur ein reguläres Speichern des Spiels (einschließlich Spiel-Autosave) hält den
aktuellen Inhalt fest. Ohne einen solchen Save wird der letzte gespeicherte Stand
wiederhergestellt. Der Codec ist automatisch getestet; ein Stromausfall oder ein
Abbruch des gesamten Spiel-Speichervorgangs wurde nicht simuliert.

Der Backpack besitzt einen eigenen Slot-Owner außerhalb des Spielerinventars.
Der Nutzer hat bestätigt, dass Polizeidurchsuchungen den Backpack ignorieren.

## Bauen und aktualisieren

Referenzumgebung aus den bisherigen Tests: Spiel 0.4.6f13, MelonLoader 0.7.3,
Mod Manager & Phone App 2.2.4; Nutzertests unter Nobara/Wine/Proton.
Das ist keine Zusage für andere Spiel- oder Manager-Versionen.
Zum Bauen: .NET SDK 8 mit Wiederherstellung der net6.0-Referenzen.
Spiel zuerst beenden, dann auf dem bisherigen Projektbranch:

```bash
cd /home/codex/Enhanced-Storage-Backpack &&
git pull --ff-only origin feature/0.1.0-backpack &&
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/" &&
cp bin/Release/net6.0/EnhancedStorageBackpack.dll \
   "/home/codex/Schreibtisch/Schedulue 1 Plugins/Mods/"
```

Bei einem neuen Checkout zuerst den Branch `feature/0.1.0-backpack` auswählen.
Spiele- und Loader-DLLs werden nur lokal referenziert und nicht mitgeliefert.
Alternative Buildparameter: `MelonLoaderDirectory`, `GameAssembliesDirectory`.
Eine einzige Version der Mod-DLL im Ordner `Mods` verwenden.
Der Mod Manager ist für seine Einstellungsoberfläche erforderlich, aber keine
fest eingebundene DLL-Abhängigkeit dieser Mod.

## Deinstallation

Vor dem Entfernen der DLL den Backpack leeren und zusätzliche Lagerplätze
leeren; Lagergrößen auf Original zurückstellen. Anschließend im Spiel speichern.
Danach das Spiel beenden und `Mods/EnhancedStorageBackpack.dll` entfernen.
Eine Sicherung des Mod-Spielstands behalten. Ohne Mod ist der Backpack nicht
zugänglich; Erhalt seiner Zusatzdaten bei späterem Speichern ohne Mod ist nicht
zugesichert.

## Diagnose und Tests

Optional: `UserData/Enhanced-Storage-Backpack-Debug.log`.
Genau eine dauerhafte Logdatei, höchstens drei protokollierte Sitzungen,
unterschieden durch `ESB_SESSION_START`; ungefähr 1 MiB pro Sitzung.
Deaktivierte Sitzungen erzeugen keine Einträge. Ein-/Ausschalten innerhalb einer
Sitzung beginnt keine neue Sitzung. Alte nummerierte Logs werden beim Umstieg
übernommen und danach entfernt. Kein zyklisches Datei-Logging.

```bash
dotnet run --project tests/StorageRulesTests.csproj -c Release
dotnet run --project tests/BackpackSaveTests.csproj -c Release
dotnet run --project tests/DiagnosticLogTests.csproj -c Release
```

[Validierung und offene Release-Prüfungen](docs/RELEASE-VALIDATION.md) ·
[Nexus-Entwurf und KI-Kennzeichnung](docs/NEXUS-RELEASE.md) ·
[Projektplan](docs/ROADMAP.md)

## Release-Paket erstellen

```bash
python3 scripts/package-release.py --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Erstellt nach erfolgreichem frischem Release-Build ein geprüftes ZIP und eine
SHA-256-Datei in `dist/`. Enthalten sind ausschließlich Mod-DLL, zweisprachige
Installationshinweise, Changelog und Hash-Manifest. Bestehende Pakete werden nicht
überschrieben. Es wird nichts automatisch installiert oder veröffentlicht.
Siehe [Release-Checkliste](docs/RELEASE-CHECKLIST.md).

## Weiterverwendung

Es gilt die [12-Monats-Regel von CoDeX-Gaming](PERMISSIONS.md). Maßgeblich ist
das letzte veröffentlichte Mod-Update, nicht der letzte GitHub-Commit.
GitHubs Plattformrechte zum Ansehen und Forken bleiben unberührt.
