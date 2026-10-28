const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) continue;
    const [key, value] = arg.slice(2).split('=');
    if (value !== undefined) {
      args[key] = value;
      continue;
    }
    const next = argv[i + 1];
    if (next && !next.startsWith('--')) {
      args[key] = next;
      i++;
    } else {
      args[key] = true;
    }
  }
  return args;
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function ensureEmptyDir(dir) {
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
}

function run(command, options = {}) {
  execSync(command, { stdio: 'inherit', shell: true, ...options });
}

function compressFolderToZip(folderPath, zipPath) {
  const folder = folderPath.replace(/'/g, "''");
  const zip = zipPath.replace(/'/g, "''");
  run(
    `powershell -NoProfile -Command "if (Test-Path '${zip}') { Remove-Item -Force '${zip}' }; Compress-Archive -Path '${folder}' -DestinationPath '${zip}' -Force"`,
  );
}

const REQUIRED_LEGAL_FILES = [
  'LICENSE',
  'UPSTREAM.md',
  'DIFFERENCES.md',
  'THIRD_PARTY_NOTICES.txt',
  'THIRD_PARTY_LICENSES.txt',
];

function portableFolderName(version, arch = 'x64') {
  return `ImplDmNote-v${version}-windows-${arch}-portable`;
}

function builtExecutablePath(repoRoot) {
  return path.join(
    repoRoot,
    'src-tauri',
    'target',
    'release',
    'impl-dm-note.exe',
  );
}

function main() {
  if (process.platform !== 'win32') {
    throw new Error('This script is intended for Windows portable builds.');
  }

  const args = parseArgs(process.argv);
  if (args.help) {
    console.log(
      [
        'Usage: node scripts/build-portable-win.js [options]',
        '',
        'Options:',
        '  --arch <x64|x86|arm64> Output name architecture (default: x64)',
        '  --exe-name <name>      Output exe name (default: impl-dm-note.exe)',
        '  --out <dir>            Output root dir (default: dist/portable)',
        '  --folder <name>        Output folder name (default: ImplDmNote-v<ver>-windows-<arch>-portable)',
        '',
      ].join('\n'),
    );
    return;
  }
  const arch = String(args.arch ?? 'x64');
  const exeName = String(
    args['exe-name'] ?? args.exeName ?? 'impl-dm-note.exe',
  );

  const repoRoot = path.resolve(__dirname, '..');
  const pkg = readJson(path.join(repoRoot, 'package.json'));
  const version = String(pkg.version ?? '0.0.0');

  const outRoot = path.resolve(
    args.out ?? path.join(repoRoot, 'dist', 'portable'),
  );
  const folderName = String(args.folder ?? portableFolderName(version, arch));
  const outDir = path.join(outRoot, folderName);
  const zipPath = path.join(outRoot, `${folderName}.zip`);

  fs.mkdirSync(outRoot, { recursive: true });

  // 1) Build binary only (no installer). The app uses the system's Evergreen
  // WebView2 Runtime, the same one the in-game viewers need.
  run('npx tauri build --no-bundle -f asio-backend', { cwd: repoRoot });

  const exeSrc = builtExecutablePath(repoRoot);
  if (!fs.existsSync(exeSrc)) {
    throw new Error(`Built exe not found: ${exeSrc}`);
  }

  // 2) Stage portable folder
  ensureEmptyDir(outDir);
  fs.copyFileSync(exeSrc, path.join(outDir, exeName));
  for (const legalFile of REQUIRED_LEGAL_FILES) {
    const source = path.join(repoRoot, legalFile);
    if (!fs.existsSync(source)) {
      throw new Error(`Required legal notice missing: ${source}`);
    }
    fs.copyFileSync(source, path.join(outDir, legalFile));
  }

  // 3) Zip
  compressFolderToZip(outDir, zipPath);

  console.log(`[portable] folder: ${outDir}`);
  console.log(`[portable] zip:    ${zipPath}`);
}

if (require.main === module) {
  try {
    main();
  } catch (err) {
    console.error(err?.stack || err?.message || String(err));
    process.exit(1);
  }
}

module.exports = {
  REQUIRED_LEGAL_FILES,
  builtExecutablePath,
  portableFolderName,
};
