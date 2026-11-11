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

test('version sync writes the app version everywhere through the one version writer', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'element-note-version-'));
  const write = (file, text) => {
    fs.mkdirSync(path.dirname(path.join(root, file)), { recursive: true });
    fs.writeFileSync(path.join(root, file), text);
  };
  const writer = 'scripts/tasks/version/version.mjs';
  write(
    writer,
    fs.readFileSync(path.join(__dirname, '../../..', writer), 'utf8'),
  );
  write('apps/element-note/package.json', '{\n  "version": "2.3.4"\n}\n');
  write('Element/Info.json', '{\n  "Version": "0.0.0"\n}\n');
  write(
    'apps/element-note/src-tauri/tauri.conf.json',
    '{\n  "version": "0.0.0"\n}\n',
  );
  write(
    'apps/element-note/src-tauri/Cargo.toml',
    '[package]\nversion = "0.0.0"\n',
  );
  write(
    'apps/element-note/src-tauri/Cargo.lock',
    '[[package]]\nname = "element-note"\nversion = "0.0.0"\n',
  );
  write(
    'package-lock.json',
    '{\n  "packages": {\n    "apps/element-note": {\n      "name": "@element/element-note",\n      "version": "0.0.0"\n    }\n  }\n}\n',
  );
  write('apps/element-note/THIRD_PARTY_LICENSES.txt', '- element-note 0.0.0\n');
  write(
    'apps/element-note/README.md',
    'upstream release URL must stay untouched\n',
  );

  const result = syncVersion(root);
  assert.equal(
    result.portableArtifact,
    'ElementNote-v2.3.4-windows-x64-portable.zip',
  );
  const read = (file) => fs.readFileSync(path.join(root, file), 'utf8');
  assert.equal(JSON.parse(read('Element/Info.json')).Version, '2.3.4');
  assert.equal(
    JSON.parse(read('apps/element-note/src-tauri/tauri.conf.json')).version,
    '2.3.4',
  );
  assert.match(
    read('apps/element-note/src-tauri/Cargo.toml'),
    /version = "2\.3\.4"/,
  );
  assert.match(
    read('apps/element-note/src-tauri/Cargo.lock'),
    /version = "2\.3\.4"/,
  );
  assert.equal(
    JSON.parse(read('package-lock.json')).packages['apps/element-note'].version,
    '2.3.4',
  );
  assert.equal(
    read('apps/element-note/THIRD_PARTY_LICENSES.txt'),
    '- element-note 2.3.4\n',
  );
  assert.equal(
    read('apps/element-note/README.md'),
    'upstream release URL must stay untouched\n',
  );
});
