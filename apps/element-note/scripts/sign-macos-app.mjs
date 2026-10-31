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
  'ElementNote.app',
);
const helperPath = path.join(
  appPath,
  'Contents',
  'Resources',
  'ElementNote.app',
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
  console.error(`ElementNote.app was not found at ${appPath}`);
  process.exit(1);
}

if (existsSync(helperPath)) {
  sign(helperPath, 'io.github.kgh1113.elementnote.helper.dock');
}
sign(appPath, 'io.github.kgh1113.elementnote');
runCodesign(['--verify', '--deep', '--strict', '--verbose=2', appPath]);

console.log('ElementNote.app signed with a stable local designated requirement.');
