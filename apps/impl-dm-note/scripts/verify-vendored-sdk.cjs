const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');

function inventory(directory, prefix = '') {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (entry.isSymbolicLink()) throw new Error('SDK source must not contain symlinks');
    const name = prefix + entry.name;
    return entry.isDirectory()
      ? inventory(path.join(directory, entry.name), name + '/')
      : [name];
  }).sort();
}

function verifySdk(root = path.resolve(__dirname, '../vendor/adofai-ipc')) {
  const metadata = JSON.parse(fs.readFileSync(path.join(root, 'source.json'), 'utf8'));
  if (metadata.source !== 'https://github.com/KGH1113/adofai-ipc' ||
      metadata.sourceDirty !== false || metadata.version !== '2.1.0' ||
      metadata.protocolVersion !== 3 || !/^[a-f0-9]{40}$/.test(metadata.sourceRevision) ||
      !metadata.files || typeof metadata.files !== 'object' || Array.isArray(metadata.files))
    throw new Error('Invalid SDK source provenance');
  const expected = Object.keys(metadata.files).sort();
  if (!expected.includes('index.ts') || !expected.includes('transport.ts') ||
      expected.some((file) => !/^(?:[a-z0-9-]+\/)*[a-z0-9-]+\.ts$/i.test(file)) ||
      JSON.stringify(inventory(path.join(root, 'src'))) !== JSON.stringify(expected))
    throw new Error('Invalid SDK source inventory');
  for (const file of expected) {
    const actual = createHash('sha256').update(fs.readFileSync(path.join(root, 'src', file))).digest('hex');
    if (actual !== metadata.files[file]) throw new Error(`Vendored SDK source mismatch: ${file}`);
  }
  const protocol = fs.readFileSync(path.join(root, 'src/models/protocol.ts'), 'utf8');
  if (!protocol.includes(`CLIENT_VERSION = "${metadata.version}"`) ||
      !protocol.includes(`PROTOCOL_VERSION = ${metadata.protocolVersion}`))
    throw new Error('SDK version differs from provenance');
  if (!fs.readFileSync(path.join(root, 'LICENSE'), 'utf8').includes('MIT License'))
    throw new Error('SDK license missing');
  return metadata;
}

if (require.main === module) {
  const metadata = verifySdk();
  console.log(`Verified ADOFAI-IPC ${metadata.version}, protocol ${metadata.protocolVersion}, ${Object.keys(metadata.files).length} source files`);
}
module.exports = { verifySdk };
