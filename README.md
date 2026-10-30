# 0.2.0-beta.5 — cash-drag UI correction (community-test beta)

The stale cash drag guard has been corrected. The equivalent singleplayer fix passed
the owner's test; native host/client validation of beta.5 is pending and will be
performed by the community. All peers require beta.5 and SteamNetworkLib 1.6.0 IL2CPP.
No save format or network message format changed. Older beta.4 results below are
historical evidence only, not validation of this build.

Build current package:
```bash
python3 scripts/package-release.py --beta --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```
Output: `dist/Enhanced-Storage-Backpack-0.2.0-beta.5.zip` and SHA-256 file.
See [current changelog](docs/CHANGELOG.md) and
[validation scope](docs/RELEASE-VALIDATION.md).

---

## Previous beta.4 documentation (historical; use beta.5 for the new update)

> **0.2.0-beta.4 — experimental multiplayer release candidate (unpublished).**
> Owner-confirmed host + one client tests passed on Schedule I 0.4.7f12,
> MelonLoader 0.7.3 and SteamNetworkLib 1.6.0 IL2CPP, using two Steam accounts
> on one Nobara PC with Wolf/Moonlight. The exact release ZIP still needs its smoke test.
> Host and every client must use the same beta. See docs/RELEASE-VALIDATION.md for scope.

# Enhanced Storage + Backpack

Storage- und Backpack-Mod für **Schedule I (IL2CPP)** von **CoDeX-Gaming**.
Mit Codex/KI entwickelter Code; Anforderungen und Spieltests durch den Projektbetreiber.

**Dieser Branch: 0.2.0-beta.4.** Bestätigt sind Rucksacktransfers mit korrekter
Menge und Qualität, Wiederbeitritt, Speichern mit vollständigem Neustart sowie
ein Large Storage Rack mit 128 Plätzen. Das ersetzt keine Prüfung aller
Modkombinationen oder von getrennten PCs/WAN.

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
- Dauerhaft eine begrenzte Debug-Datei mit den letzten drei protokollierten Spielsitzungen.

Die sichtbare Gegenstandsdarstellung eines Regals ist durch dessen Stellfläche
begrenzt; 128 Plätze bedeuten nicht 128 gleichzeitig sichtbare Gegenstandsmodelle.
Multiplayer ist auf diesem Branch experimentell. Mono wird nicht unterstützt.

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

Referenzumgebung aus den bisherigen Tests: Spiel 0.4.7f12, MelonLoader 0.7.3,
Mod Manager & Phone App 2.2.4; Nutzertests unter Nobara/Wine/Proton.
Das ist keine Zusage für andere Spiel- oder Manager-Versionen.
Zum Bauen: .NET SDK 8 mit Wiederherstellung der net6.0-Referenzen.
Spiel zuerst beenden, dann auf dem bisherigen Projektbranch:

```bash
cd /home/codex/Enhanced-Storage-Backpack &&
git pull --ff-only origin feature/0.2.0-multiplayer-beta &&
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/" &&
cp bin/Release/net6.0/EnhancedStorageBackpack.dll \
   "/home/codex/Schreibtisch/Schedulue 1 Plugins/Mods/"
```

Bei einem neuen Checkout zuerst den Branch `feature/0.2.0-multiplayer-beta` auswählen.
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

Immer aktiv: `UserData/Enhanced-Storage-Backpack-Debug.log`.
Die Datei hält höchstens drei Sitzungen mit ungefähr 1 MiB pro Sitzung vor;
`ESB_SESSION_START` trennt die Sitzungen. Alte DebugLogging-Einstellungen
schalten die Diagnose nicht mehr aus. Bei Fehlern beide Logs (Latest.log und
Debug.log) von Host und Client aus derselben Testsitzung beilegen.

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
python3 scripts/package-release.py --beta --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
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

## Multiplayer-Beta paketieren

Mit beendetem Spiel und installierter IL2CPP-Abhängigkeit:

```bash
python3 scripts/package-release.py --beta --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Ergebnis: `dist/Enhanced-Storage-Backpack-0.2.0-beta.4.zip` und SHA-256-Datei.
Der Packager baut net6.0 neu, kopiert nur freigegebene Moddateien und legt die
Beta-Anleitung/Rückmeldevorlage bei. Keine Spiel- oder SteamNetworkLib-DLL wird
mitgeliefert. Vorhandene Archive werden nicht überschrieben.

Neue Befehls-/Receipt-/Geldprüfungen: `dotnet run --project tests/BackpackCommandTests.csproj`.
Weitere Multiplayer-Prüfprojekte stehen unter `tests`; sie simulieren keine nativen Peers.
