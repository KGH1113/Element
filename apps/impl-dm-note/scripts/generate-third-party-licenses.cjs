const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const appRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(appRoot, '..', '..');
const outputPath = path.join(appRoot, 'THIRD_PARTY_LICENSES.txt');

function runCargoAbout() {
  const version = spawnSync('cargo', ['about', '--version'], {
    encoding: 'utf8',
  });
  if (version.status !== 0 || !/cargo-about 0\.9\.2\b/.test(version.stdout)) {
    throw new Error('cargo-about 0.9.2 is required');
  }

  const result = spawnSync(
    'cargo',
    ['about', 'generate', '../about.hbs', '--config', '../about.toml'],
    { cwd: path.join(appRoot, 'src-tauri'), encoding: 'utf8' },
  );
  if (result.status !== 0) {
    throw new Error(result.stderr || 'cargo-about failed');
  }
  return result.stdout.replace(/\r\n/g, '\n').trimEnd();
}

function findLicenseText(packageDir) {
  if (!fs.existsSync(packageDir)) return null;
  const licenseName = fs
    .readdirSync(packageDir)
    .filter((name) => /^(licen[cs]e|copying|notice)(\.|$)/i.test(name))
    .sort((a, b) => a.localeCompare(b, 'en'))[0];
  return licenseName
    ? fs
        .readFileSync(path.join(packageDir, licenseName), 'utf8')
        .replace(/\r\n/g, '\n')
        .trim()
    : null;
}

function generateNpmReport() {
  const lock = JSON.parse(
    fs.readFileSync(path.join(repoRoot, 'package-lock.json'), 'utf8'),
  );
  const packages = [];

  for (const [lockPath, metadata] of Object.entries(lock.packages ?? {})) {
    if (
      !lockPath.startsWith('node_modules/') ||
      metadata.dev === true ||
      !metadata.version
    )
      continue;
    const packageDir = path.join(repoRoot, lockPath);
    const packageJsonPath = path.join(packageDir, 'package.json');
    if (!fs.existsSync(packageJsonPath)) continue;
    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
    packages.push({
      name: packageJson.name ?? lockPath.slice('node_modules/'.length),
      version: String(metadata.version),
      license: packageJson.license ?? metadata.license ?? 'UNKNOWN',
      homepage: packageJson.homepage ?? packageJson.repository?.url ?? '',
      copyright: packageJson.author ?? packageJson.contributors ?? '',
      text: findLicenseText(packageDir),
    });
  }

  packages.sort((a, b) =>
    `${a.name}@${a.version}`.localeCompare(`${b.name}@${b.version}`, 'en'),
  );
  const lines = [
    'NPM PRODUCTION DEPENDENCY LICENSES',
    '===================================',
  ];
  for (const entry of packages) {
    lines.push(
      '',
      '-------------------------------------------------------------------------------',
    );
    lines.push(`${entry.name} ${entry.version}`);
    lines.push(
      `License: ${typeof entry.license === 'string' ? entry.license : JSON.stringify(entry.license)}`,
    );
    if (entry.homepage) lines.push(`Source: ${entry.homepage}`);
    if (entry.copyright) {
      lines.push(
        `Copyright metadata: ${typeof entry.copyright === 'string' ? entry.copyright : JSON.stringify(entry.copyright)}`,
      );
    }
    lines.push(
      '',
      entry.text ??
        'No standalone license file was published in this npm package.',
    );
  }
  return lines.join('\n');
}

function generateVendoredReport() {
  const root = path.join(appRoot, 'vendor', 'adofai-ipc');
  const metadata = JSON.parse(fs.readFileSync(path.join(root, 'source.json'), 'utf8'));
  return ['VENDORED SOURCE LICENSES', '========================', '', `ADOFAI-IPC TypeScript SDK ${metadata.version}`, `Source: ${metadata.source}`, 'License: MIT', '', fs.readFileSync(path.join(root, 'LICENSE'), 'utf8').trim()].join('\n');
}

function generateReport() {
  return [
    'ImplDmNote Third-Party Licenses',
    'Generated deterministically from Cargo.lock and package-lock.json.',
    '',
    runCargoAbout(),
    '',
    generateNpmReport(),
    '',
    generateVendoredReport(),
    '',
  ].join('\n');
}

const report = generateReport();
if (process.argv.includes('--check')) {
  const existing = fs.existsSync(outputPath)
    ? fs.readFileSync(outputPath, 'utf8')
    : '';
  if (existing !== report) {
    console.error('THIRD_PARTY_LICENSES.txt is out of date.');
    process.exit(1);
  }
} else {
  fs.writeFileSync(outputPath, report);
  console.log(`Wrote ${path.relative(repoRoot, outputPath)}`);
}

module.exports = { generateNpmReport, generateReport };
