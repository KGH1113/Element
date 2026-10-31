const fs = require('node:fs');
const path = require('node:path');

function syncVersion(appRoot = path.resolve(__dirname, '..')) {
  const packagePath = path.join(appRoot, 'package.json');
  const tauriConfigPath = path.join(appRoot, 'src-tauri', 'tauri.conf.json');
  const cargoTomlPath = path.join(appRoot, 'src-tauri', 'Cargo.toml');
  const packageJson = JSON.parse(fs.readFileSync(packagePath, 'utf8'));
  const version = String(packageJson.version);

  const tauriConfig = JSON.parse(fs.readFileSync(tauriConfigPath, 'utf8'));
  tauriConfig.version = version;
  fs.writeFileSync(
    tauriConfigPath,
    `${JSON.stringify(tauriConfig, null, 2)}\n`,
  );

  const cargoToml = fs
    .readFileSync(cargoTomlPath, 'utf8')
    .replace(/^version = ".*"$/m, `version = "${version}"`);
  fs.writeFileSync(cargoTomlPath, cargoToml);

  return {
    version,
    portableArtifact: `ElementNote-v${version}-windows-x64-portable.zip`,
  };
}

if (require.main === module) {
  const result = syncVersion();
  console.log(`Synchronized ElementNote ${result.version}.`);
  console.log(`Windows artifact: ${result.portableArtifact}`);
}

module.exports = { syncVersion };
