> Historical stable singleplayer 0.1.6 material below.
> For the additional 0.2.0-beta.1 multiplayer upload use MULTIPLAYER-BETA.md,
> BETA-FEEDBACK.md and the current CHANGELOG.md. Do not replace the stable file.
> Mark the additional download experimental; native two-peer validation is pending.

# Nexus release checklist — 0.1.6

## Prepared

- [x] Normal gameplay tests documented with limits in RELEASE-VALIDATION.md.
- [x] README, DE/EN installation/removal instructions and changelog updated.
- [x] DE/EN Nexus descriptions with explicit AI disclosure prepared.
- [x] Fresh-build packaging script with file allowlist, archive verification and SHA-256.
- [x] Runtime code remains the user-tested 0.1.6.

## Build on Nobara

Close the game, then:

```bash
cd /home/codex/Enhanced-Storage-Backpack &&
git pull --ff-only origin feature/0.1.0-backpack &&
python3 scripts/package-release.py --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Requires Python 3 and the same .NET SDK used for the successful game builds.
Outputs:
- dist/Enhanced-Storage-Backpack-0.1.6.zip
- dist/Enhanced-Storage-Backpack-0.1.6.zip.sha256

Verify checksum from dist: `sha256sum -c Enhanced-Storage-Backpack-0.1.6.zip.sha256`.
ZIP must contain exactly:
- Mods/EnhancedStorageBackpack.dll
- INSTALL-DE-EN.txt
- CHANGELOG.md
- PERMISSIONS.md
- manifest.json

The script refuses to overwrite an existing package. Move an earlier package
outside dist before deliberately creating a replacement; no automatic deletion.
No game/loader/manager DLLs, logs, saves, preferences, debug symbols or source
inspection artifacts are distributed. Do not package a compiler-check DLL.

## Final package test

- [x] Build exits successfully; ZIP integrity and checksum pass.
- [x] With game closed, install the DLL extracted from this exact ZIP.
- [x] Startup reports 0.1.6; open backpack and a paginated storage.
- [x] Save, complete restart, load: item types and quantities retained.
- [x] Uploaded ZIP independently checked; documentation-only repack preserves tested DLL bytes.

## Real screenshots

Capture from 0.1.6 with debug/console overlays closed:
- [ ] Backpack with visible items, navigation and Finish button.
- [ ] Storage with 128 slots / 5 rows, showing the compact final page.
- [ ] Our settings page in German.
- [ ] Our settings page in English.

Older supplied screenshots show intermediate UI states and are not used as final
marketing images. No invented gameplay screenshots or performance claims.

## Nexus form

- Name: Enhanced Storage + Backpack.
- Version: 0.1.6. Game: Schedule I. State clearly: IL2CPP, singleplayer only.
- Requirement: MelonLoader; Mod Manager & Phone App for the in-game settings UI.
- Tags: AI-Generated Content; AI Media when using our generated page description.
- Description: use German/English sections of NEXUS-RELEASE.md.
- Source link: GitHub repository. No claim that a public repo alone grants reuse rights.
- [x] Custom permission rule agreed and documented in PERMISSIONS.md (12 months since last mod release).
- [ ] Confirm those choices match any existing licenses/third-party permissions.
- [ ] Recheck current Nexus guidelines at upload time.
- [ ] Upload tested ZIP and current screenshots, review page, publish.

Nexus upload and publication are performed by the project owner.
GitHub release/tag publication remains a separate step.
