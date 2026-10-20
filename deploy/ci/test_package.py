import json
import os
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import unittest


class PackageTests(unittest.TestCase):
    def test_omits_runtime_credentials_but_preserves_assemblies(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            publish = root / 'publish'
            publish.mkdir()
            for name in ('appsettings.json', 'appsettings.Production.json', '.env', 'signing.pfx'):
                (publish / name).write_text('private-value-that-must-not-be-packaged')
            (publish / 'Example.dll').write_bytes(b'assembly')
            (publish / 'Example.deps.json').write_text('{}')
            output = root / 'release.tar.gz'
            result = subprocess.run([sys.executable, str(Path(__file__).with_name('package.py')),
                                     str(publish), str(output), '--component', 'api'],
                                    env=dict(os.environ, GITHUB_SHA='a' * 40), capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr.decode())
            with tarfile.open(output) as archive:
                self.assertEqual(set(archive.getnames()),
                                 {'appsettings.json', 'Example.dll', 'Example.deps.json', 'release.json'})
                self.assertEqual(json.load(archive.extractfile('appsettings.json')), {})
                for member in archive.getmembers():
                    self.assertNotIn(b'private-value-that-must-not-be-packaged', archive.extractfile(member).read())


if __name__ == '__main__':
    unittest.main()
