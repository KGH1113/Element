import { existsSync } from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

if (process.platform !== 'darwin') {
  process.exit(0);
}

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const tauriDir = path.resolve(scriptDir, '..', 'src-tauri');
const profile = process.argv.includes('--debug') ? 'debug' : 'release';
const appPath = path.join(
  tauriDir,
  'target',
  profile,
  'bundle',
  'macos',
  'ImplDmNote.app',
);
const helperPath = path.join(
  appPath,
  'Contents',
  'Resources',
  'ImplDmNote.app',
);

function runCodesign(args) {
  const result = spawnSync('codesign', args, { stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function sign(bundlePath, identifier) {
  runCodesign([
    '--force',
    '--sign',
    '-',
    '--identifier',
    identifier,
    '--requirements',
    `=designated => identifier "${identifier}"`,
    bundlePath,
  ]);
}

if (!existsSync(appPath)) {
  console.error(`ImplDmNote.app was not found at ${appPath}`);
  process.exit(1);
}

if (existsSync(helperPath)) {
  sign(helperPath, 'io.github.kgh1113.impldmnote.helper.dock');
}
sign(appPath, 'io.github.kgh1113.impldmnote');
runCodesign(['--verify', '--deep', '--strict', '--verbose=2', appPath]);

console.log('ImplDmNote.app signed with a stable local designated requirement.');
