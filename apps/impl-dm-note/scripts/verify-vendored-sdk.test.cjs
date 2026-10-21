const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { verifySdk } = require('./verify-vendored-sdk.cjs');

test('the pinned SDK includes both public entry points', () => {
  const sdk = verifySdk();
  assert.equal(sdk.version, '2.1.0');
  assert.ok(sdk.files['index.ts']);
  assert.ok(sdk.files['transport.ts']);
});

for (const [name, mutate, error] of [
  ['modified source', (root) => fs.appendFileSync(path.join(root, 'src/transport.ts'), '\n// changed'), /source mismatch/],
  ['missing source', (root) => fs.unlinkSync(path.join(root, 'src/transport.ts')), /inventory/],
  ['extra source', (root) => fs.writeFileSync(path.join(root, 'src/untracked.ts'), ''), /inventory/],
  ['unsafe path', (root) => {
    const file = path.join(root, 'source.json');
    const metadata = JSON.parse(fs.readFileSync(file, 'utf8'));
    metadata.files['../outside.ts'] = '0'.repeat(64);
    fs.writeFileSync(file, JSON.stringify(metadata));
  }, /inventory/],
]) {
  test(`SDK verification rejects ${name}`, () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'impl-ipc-sdk-'));
    try {
      fs.cpSync(path.resolve(__dirname, '../vendor/adofai-ipc'), root, { recursive: true });
      mutate(root);
      assert.throws(() => verifySdk(root), error);
    } finally {
      fs.rmSync(root, { recursive: true, force: true });
    }
  });
}
