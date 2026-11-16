// npm's `version` hook: `npm version <x.y.z|patch|minor|major>` here has just
// written the new version to package.json. This writes it everywhere else the
// mod and ElementNote keep their one shared version, through the repository's
// only version writer, scripts/tasks/version/version.mjs (the same one as
// `./scripts/run.sh version <x.y.z>`).
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

function syncVersion(repositoryRoot = path.resolve(__dirname, '../../..')) {
  const packagePath = path.join(
    repositoryRoot,
    'apps',
    'element-note',
    'package.json',
  );
  const version = String(
    JSON.parse(fs.readFileSync(packagePath, 'utf8')).version,
  );
  execFileSync(
    process.execPath,
    [
      path.join(repositoryRoot, 'scripts', 'tasks', 'version', 'version.mjs'),
      'set',
      version,
    ],
    { stdio: 'inherit' },
  );

  return {
    version,
    portableArtifact: `ElementNote-v${version}-windows-x64-portable.zip`,
  };
}

if (require.main === module) {
  const result = syncVersion();
  console.log(`Windows artifact: ${result.portableArtifact}`);
}

module.exports = { syncVersion };
