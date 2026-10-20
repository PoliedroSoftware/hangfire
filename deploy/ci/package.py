"""Package a published .NET service without configuration or credentials."""
import argparse
import json
import os
from pathlib import Path
import re
import tarfile

parser = argparse.ArgumentParser()
parser.add_argument('publish', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--component', choices=['api', 'hangfire'], required=True)
args = parser.parse_args()
revision = os.environ['GITHUB_SHA']
assert re.fullmatch('[0-9a-f]{40}', revision)
root = args.publish.resolve()
assert root.is_dir()
for item in root.rglob('*'):
    assert not item.is_symlink(), 'Published package must not contain symbolic links'
    if item.is_file() and (any(part.startswith('.') for part in item.relative_to(root).parts) or
                           item.name.lower().startswith('appsettings') or
                           item.suffix.lower() in ('.pfx', '.p12', '.pem', '.key', '.env')):
        item.unlink()
(root / 'appsettings.json').write_text('{}\n', encoding='utf-8')
(root / 'release.json').write_text(json.dumps({'component': args.component, 'revision': revision}), encoding='utf-8')
with tarfile.open(args.output, 'w:gz') as archive:
    for item in sorted(root.rglob('*')):
        if item.is_file():
            archive.add(item, arcname=item.relative_to(root).as_posix(), recursive=False)
print(f'Packaged {args.component} {revision}; appsettings replaced with an empty object.')
