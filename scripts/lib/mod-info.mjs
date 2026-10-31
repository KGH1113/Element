#!/usr/bin/env node
// The metadata an installed mod and a release carry:
//   mod-info.mjs version  <Info.json>                       prints the version
//   mod-info.mjs launcher <Info.json> <out Info.json>       root Info.json that starts the launcher
//   mod-info.mjs state    <version> <out state.json>        Runtime/state.json for a fresh install
//   mod-info.mjs manifest <Info.json> <out> <os>=<zip>...   Element.update.json
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const MOD = 'Element';
const LAUNCHER = 'Element.Launcher.dll';
const LAUNCHER_ENTRY = 'Element.Launcher.Entry.Load';
const PLATFORMS = new Set(['macos', 'windows']);
const SEMVER = /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

function fail(message) {
  console.error(`Error: ${message}`);
  process.exit(1);
}

function readInfo(file) {
  const info = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
  if (!SEMVER.test(info.Version ?? '')) fail(`${file} has no version like 1.2.3 (found ${info.Version}).`);
  return info;
}

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
}

function sha256(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}

const [command, ...args] = process.argv.slice(2);
switch (command) {
  case 'version':
    console.log(readInfo(args[0]).Version);
    break;
  case 'launcher': {
    const [source, destination] = args;
    const info = readInfo(source);
    info.AssemblyName = `Runtime/launchers/${info.Version}/${LAUNCHER}`;
    info.EntryMethod = LAUNCHER_ENTRY;
    writeJson(destination, info);
    break;
  }
  case 'state': {
    const [version, destination] = args;
    if (!SEMVER.test(version ?? '')) fail(`Invalid version: ${version}`);
    writeJson(destination, { SchemaVersion: 1, Current: version, Previous: null, Trial: null, Failed: [] });
    break;
  }
  case 'manifest': {
    const [source, destination, ...pairs] = args;
    const { Version: version } = readInfo(source);
    const packages = {};
    for (const pair of pairs) {
      const [platform, zip] = pair.split('=');
      if (!PLATFORMS.has(platform)) fail(`Unknown package platform: ${platform}`);
      const asset = `${MOD}-${platform}.zip`;
      if (path.basename(zip) !== asset) fail(`The ${platform} package must be named ${asset}: ${zip}`);
      packages[platform] = {
        asset,
        bytes: fs.statSync(zip).size,
        sha256: sha256(zip),
        runtimePath: `${MOD}/Runtime/versions/${version}`,
      };
    }
    if (Object.keys(packages).length === 0) fail('The manifest needs at least one package.');
    writeJson(destination, { schemaVersion: 1, version, packages });
    break;
  }
  default:
    fail(`Unknown command: ${command}`);
}
