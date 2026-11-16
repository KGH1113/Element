import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const upstream = process.argv[2] && path.resolve(process.argv[2]);
const output = process.argv[3] && path.resolve(process.argv[3]);
if (!upstream || upstream === root || !output)
  throw new Error('Specify the local adofai-ipc repository and its built Release DLL directory. Build upstream with ./scripts/run.sh package first.');
const git = (...args) => execFileSync('git', ['-C', upstream, ...args], { encoding: 'utf8' }).trim();
if (git('status', '--porcelain')) throw new Error('Commit upstream changes before syncing IPC.');
const revision = git('rev-parse', 'HEAD');
const sdk = path.join(upstream, 'packages/adofai-ipc');
const version = JSON.parse(fs.readFileSync(path.join(sdk, 'package.json'), 'utf8')).version;
if (version !== '2.1.0') throw new Error('Review compatibility before changing the pinned IPC version.');
const runtime = path.join(root, 'vendor/adofai-ipc-runtime');
const files = ['AdofaiIpc.Contracts.dll', 'AdofaiIpc.Loader.dll', 'ipc/AdofaiIpc.Runtime.dll', 'ipc/manifest.json'];
const inputs = Object.fromEntries(files.map(file => [file, file === 'ipc/manifest.json'
  ? path.join(upstream, 'AdofaiIpc/src/AdofaiIpc.Runtime/manifest.json')
  : path.join(output, path.basename(file))]));
for (const file of Object.values(inputs)) fs.accessSync(file, fs.constants.R_OK);
const manifest = JSON.parse(fs.readFileSync(inputs['ipc/manifest.json'], 'utf8'));
if (manifest.RuntimeVersion !== version || manifest.ContractMajor !== 1 || manifest.WireMajor !== 3)
  throw new Error('Unexpected IPC bundle contract or wire version.');
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
for (const file of files) {
  fs.mkdirSync(path.dirname(path.join(runtime, file)), { recursive: true });
  fs.copyFileSync(inputs[file], path.join(runtime, file));
}
fs.copyFileSync(path.join(upstream, 'LICENSE'), path.join(runtime, 'LICENSE'));
const source = 'https://github.com/KGH1113/adofai-ipc';
const writeJson = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
writeJson(path.join(runtime, 'source.json'), {
  source, sourceRevision: revision, version, contractMajor: 1,
  contractAssemblyVersion: manifest.ContractAssemblyVersion, wireMajor: 3,
});
fs.writeFileSync(path.join(runtime, 'SHA256SUMS'), files.map(file => `${hash(path.join(runtime, file))}  ${file}\n`).join(''));

const destination = path.join(root, 'apps/element-note/vendor/adofai-ipc');
fs.rmSync(path.join(destination, 'src'), { recursive: true, force: true });
fs.cpSync(path.join(sdk, 'src'), path.join(destination, 'src'), { recursive: true });
fs.copyFileSync(path.join(sdk, 'LICENSE'), path.join(destination, 'LICENSE'));
function inventory(directory, prefix = '') {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const name = prefix + entry.name;
    return entry.isDirectory() ? inventory(path.join(directory, entry.name), name + '/') : [name];
  }).sort();
}
writeJson(path.join(destination, 'source.json'), {
  source, sourceRevision: revision, sourceDirty: false, version, protocolVersion: 3,
  files: Object.fromEntries(inventory(path.join(destination, 'src')).map(file => [file, hash(path.join(destination, 'src', file))])),
});
console.log(`Synced ADOFAI-IPC ${version} at ${revision}.`);
