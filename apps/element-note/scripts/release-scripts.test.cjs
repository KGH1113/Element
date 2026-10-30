const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {
  REQUIRED_LEGAL_FILES,
  builtExecutablePath,
  portableFolderName,
} = require('./build-portable-win.cjs');
const { syncVersion } = require('./sync-version.cjs');

test('portable artifact uses the ElementNote identity and Rust binary path', () => {
  assert.equal(
    portableFolderName('1.2.3'),
    'ElementNote-v1.2.3-windows-x64-portable',
  );
  assert.equal(
    builtExecutablePath('C:\\repo'),
    path.join('C:\\repo', 'src-tauri', 'target', 'release', 'element-note.exe'),
  );
  assert.deepEqual(REQUIRED_LEGAL_FILES, [
    'LICENSE',
    'UPSTREAM.md',
    'DIFFERENCES.md',
    'THIRD_PARTY_NOTICES.txt',
    'THIRD_PARTY_LICENSES.txt',
  ]);
});

test('version sync changes only package, Cargo, and Tauri identities', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'element-note-version-'));
  fs.mkdirSync(path.join(root, 'src-tauri'));
  fs.writeFileSync(path.join(root, 'package.json'), '{"version":"2.3.4"}\n');
  fs.writeFileSync(
    path.join(root, 'src-tauri', 'tauri.conf.json'),
    '{"version":"0.0.0"}\n',
  );
  fs.writeFileSync(
    path.join(root, 'src-tauri', 'Cargo.toml'),
    '[package]\nversion = "0.0.0"\n',
  );
  fs.writeFileSync(
    path.join(root, 'README.md'),
    'upstream release URL must stay untouched\n',
  );

  const result = syncVersion(root);
  assert.equal(
    result.portableArtifact,
    'ElementNote-v2.3.4-windows-x64-portable.zip',
  );
  assert.equal(
    JSON.parse(fs.readFileSync(path.join(root, 'src-tauri', 'tauri.conf.json')))
      .version,
    '2.3.4',
  );
  assert.match(
    fs.readFileSync(path.join(root, 'src-tauri', 'Cargo.toml'), 'utf8'),
    /version = "2\.3\.4"/,
  );
  assert.equal(
    fs.readFileSync(path.join(root, 'README.md'), 'utf8'),
    'upstream release URL must stay untouched\n',
  );
});
