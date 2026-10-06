const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const root = path.resolve(__dirname, '../vendor/adofai-ipc');
const metadata = JSON.parse(fs.readFileSync(path.join(root, 'source.json'), 'utf8'));
if (metadata.sourceDirty !== false || metadata.version !== '1.0.0' || metadata.protocolVersion !== 3 || !/^[a-f0-9]{40}$/.test(metadata.sourceRevision)) throw new Error('Invalid SDK source provenance');
for (const [file, expected] of Object.entries(metadata.files)) {
  if (!/^[a-z0-9-]+\.ts$/i.test(file)) throw new Error('Invalid SDK source filename');
  const actual = createHash('sha256').update(fs.readFileSync(path.join(root, 'src', file))).digest('hex');
  if (actual !== expected) throw new Error(`Vendored SDK source mismatch: ${file}`);
}
if (!fs.readFileSync(path.join(root, 'LICENSE'), 'utf8').includes('MIT License')) throw new Error('SDK license missing');
console.log(`Verified ADOFAI-IPC ${metadata.version}, protocol ${metadata.protocolVersion}, ${Object.keys(metadata.files).length} raw source files`);
