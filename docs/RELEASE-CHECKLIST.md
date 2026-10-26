# 0.2.0-beta.4 release checklist

- [x] Owner-confirmed host + one client tests documented in RELEASE-VALIDATION.md.
- [x] Backpack quantities/quality, rejoin and save/full restart confirmed.
- [x] Shared Large Storage Rack with 128 slots confirmed.
- [x] Always-on diagnostics and optional Updates Checker metadata documented.
- [x] Packaging allowlist/dependency/version checks tested with a simulated build.
- [ ] Build the distributable net6.0 ZIP on the game-equipped PC.
- [ ] Verify the generated SHA-256.
- [ ] Extract that ZIP's DLL into both peers' Mods folders with games closed.
- [ ] Smoke-test B, one item transfer, regular save and reload on both peers.
- [ ] Upload as an experimental multiplayer beta after the owner's decision.

## Build

```bash
cd /home/codex/Enhanced-Storage-Backpack &&
git switch feature/0.2.0-multiplayer-beta &&
git pull --ff-only origin feature/0.2.0-multiplayer-beta &&
python3 scripts/package-release.py --beta \
  --game-directory="/home/codex/Schreibtisch/Schedulue 1 Plugins/" &&
cd dist &&
sha256sum -c Enhanced-Storage-Backpack-0.2.0-beta.4.zip.sha256
```

The packager refuses to overwrite an existing ZIP or checksum; move an old package
aside before rebuilding. It does not install or publish anything.
Do not distribute simulated test DLLs, reference-compiler output, game DLLs or dependencies.
Host and all clients require beta.4 plus SteamNetworkLib 1.6.0 IL2CPP.
Preserve the separate stable download. Use NEXUS-RELEASE.md's beta.4 draft;
historical singleplayer copy is not the beta description.
