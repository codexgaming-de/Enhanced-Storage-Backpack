#!/usr/bin/env python3
"""Build the net6.0 release and package only explicitly approved mod files."""
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-directory', required=True)
    parser.add_argument('--beta', action='store_true', help='Explicitly package an experimental multiplayer beta')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    game = Path(args.game_directory).expanduser().resolve()
    project = root / 'EnhancedStorageBackpack.csproj'
    xml = ET.parse(project).getroot()
    version = xml.findtext('.//Version')
    pattern = r'\d+\.\d+\.\d+-beta\.\d+' if args.beta else r'\d+\.\d+\.\d+'
    if xml.findtext('.//TargetFramework') != 'net6.0' or not re.fullmatch(pattern, version or ''):
        raise RuntimeError('Unexpected release version or target framework.')
    protocol = (root / 'src/MultiplayerProtocol.cs').read_text()
    if f'Build = "{version}"' not in protocol:
        raise RuntimeError('Project and multiplayer protocol versions differ.')
    refs = ['MelonLoader/net6/MelonLoader.dll', 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll']
    if args.beta:
        refs.append('UserLibs/SteamNetworkLib.dll')
    for ref in refs:
        if not (game / ref).is_file():
            raise RuntimeError(f'Missing build reference: {game / ref}')
    output = root / 'dist'
    output.mkdir(exist_ok=True)
    archive = output / f'Enhanced-Storage-Backpack-{version}.zip'
    checksum = Path(str(archive) + '.sha256')
    if archive.exists() or checksum.exists():
        raise RuntimeError(f'Output already exists; move the previous package before rebuilding: {archive}')
    with tempfile.TemporaryDirectory(prefix='esb-release-') as temporary:
        staging = Path(temporary)
        build = staging / 'build'
        subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', '-t:Rebuild',
                        '-o', str(build), f'-p:GameDirectory={game}'], cwd=root, check=True)
        dll = build / 'EnhancedStorageBackpack.dll'
        if not dll.is_file() or dll.stat().st_size == 0:
            raise RuntimeError('Build did not produce the mod DLL.')
        files = {
            'Mods/EnhancedStorageBackpack.dll': dll.read_bytes(),
            'INSTALL-DE-EN.txt': (root / 'docs/INSTALL-DE-EN.txt').read_bytes(),
            'CHANGELOG.md': (root / 'docs/CHANGELOG.md').read_bytes(),
            'PERMISSIONS.md': (root / 'PERMISSIONS.md').read_bytes(),
        }
        if args.beta:
            for name in ('MULTIPLAYER-BETA.md', 'BETA-FEEDBACK.md'):
                files[name] = (root / 'docs' / name).read_bytes()
        manifest = {'version': version, 'targetFramework': 'net6.0',
                    'channel': 'experimental-multiplayer-beta' if args.beta else 'stable',
                    'nativeMultiplayerTested': True if args.beta else None,
                    'nativeTestScope': 'Owner-confirmed two-client core tests on one PC; gameplay baseline fefe743' if args.beta else None,
                    'exactPackagedDllTested': False,
                    'externalDependencies': ['SteamNetworkLib 1.6.0 IL2CPP (UserLibs)'] if args.beta else [],
                    'files': {name: hashlib.sha256(data).hexdigest() for name, data in files.items()}}
        files['manifest.json'] = (json.dumps(manifest, indent=2) + '\n').encode()
        packed = staging / archive.name
        with ZipFile(packed, 'w', ZIP_DEFLATED) as z:
            for name, data in files.items():
                z.writestr(name, data)
        with ZipFile(packed) as z:
            if z.testzip() is not None or set(z.namelist()) != set(files):
                raise RuntimeError('Archive integrity or file list validation failed.')
            for name, data in files.items():
                if z.read(name) != data:
                    raise RuntimeError(f'Archive content mismatch: {name}')
        package_data = packed.read_bytes()
        # Exclusive creation prevents accidentally overwriting a reviewed package.
        with archive.open('xb') as f:
            f.write(package_data)
        checksum.write_text(hashlib.sha256(package_data).hexdigest() + '  ' + archive.name + '\n')
    print(f'Package: {archive}\nSHA-256: {checksum}')
    print('Test the DLL extracted from this ZIP in-game before uploading. Nothing was installed or published.')


if __name__ == '__main__':
    main()
