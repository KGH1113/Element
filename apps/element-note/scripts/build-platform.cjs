const { spawnSync } = require('node:child_process');

const script =
  process.platform === 'darwin'
    ? 'tauri:build:mac'
    : process.platform === 'win32'
      ? 'tauri:build:windows'
      : null;

if (!script) {
  console.error(
    `Desktop release builds are unsupported on ${process.platform}.`,
  );
  process.exit(1);
}

const result = spawnSync('npm', ['run', script], {
  cwd: process.cwd(),
  stdio: 'inherit',
  shell: process.platform === 'win32',
});

process.exit(result.status ?? 1);
