"""Packaging checks with a simulated build; does not validate a game DLL."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from zipfile import ZipFile

SOURCE = Path(__file__).resolve().parents[1]


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'repo'
        self.game = Path(self.temp.name) / 'game'
        for name in ['scripts/package-release.py', 'EnhancedStorageBackpack.csproj',
                     'src/MultiplayerProtocol.cs', 'PERMISSIONS.md', 'docs/INSTALL-DE-EN.txt',
                     'docs/CHANGELOG.md', 'docs/MULTIPLAYER-BETA.md', 'docs/BETA-FEEDBACK.md']:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(SOURCE / name, target)
        for name in ['MelonLoader/net6/MelonLoader.dll',
                     'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll', 'UserLibs/SteamNetworkLib.dll']:
            target = self.game / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b'reference-not-for-distribution')
        spec = importlib.util.spec_from_file_location('packager', self.root / 'scripts/package-release.py')
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)

    def tearDown(self):
        self.temp.cleanup()

    def run_package(self, beta=True, fail=False):
        def build(args, **kwargs):
            if fail:
                raise subprocess.CalledProcessError(1, args)
            output = Path(args[args.index('-o') + 1])
            output.mkdir(parents=True)
            (output / 'EnhancedStorageBackpack.dll').write_bytes(b'simulated-mod-build')
            (output / 'Assembly-CSharp.dll').write_bytes(b'forbidden-game-assembly')
            (output / 'SteamNetworkLib.dll').write_bytes(b'forbidden-dependency')
        args = ['package-release.py', '--game-directory', str(self.game)] + (['--beta'] if beta else [])
        with patch.object(sys, 'argv', args), patch.object(self.module.subprocess, 'run', build):
            self.module.main()

    def test_allowlist_and_manifest(self):
        self.run_package()
        archive = next((self.root / 'dist').glob('*.zip'))
        with ZipFile(archive) as z:
            self.assertEqual(set(z.namelist()), {'Mods/EnhancedStorageBackpack.dll', 'INSTALL-DE-EN.txt',
                'CHANGELOG.md', 'PERMISSIONS.md', 'MULTIPLAYER-BETA.md', 'BETA-FEEDBACK.md', 'manifest.json'})
            manifest = json.loads(z.read('manifest.json'))
            self.assertEqual(manifest['channel'], 'experimental-multiplayer-beta')
            self.assertFalse(manifest['nativeMultiplayerTested'])
            self.assertIsNone(z.testzip())
        self.assertTrue(Path(str(archive) + '.sha256').is_file())

    def test_beta_flag_required(self):
        with self.assertRaises(RuntimeError): self.run_package(beta=False)

    def test_dependency_required(self):
        (self.game / 'UserLibs/SteamNetworkLib.dll').unlink()
        with self.assertRaises(RuntimeError): self.run_package()

    def test_no_overwrite(self):
        self.run_package()
        with self.assertRaises(RuntimeError): self.run_package()

    def test_failed_build_produces_no_archive(self):
        with self.assertRaises(subprocess.CalledProcessError): self.run_package(fail=True)
        self.assertFalse(list((self.root / 'dist').glob('*.zip')))

    def test_version_mismatch_rejected(self):
        (self.root / 'src/MultiplayerProtocol.cs').write_text('Build = "0.2.0-beta.999";')
        with self.assertRaises(RuntimeError): self.run_package()


if __name__ == '__main__':
    unittest.main()
