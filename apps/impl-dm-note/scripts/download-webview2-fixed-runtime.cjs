const fs = require('fs');
const https = require('https');
const path = require('path');
const os = require('os');
const { execSync } = require('child_process');

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) continue;
    const [key, value] = arg.slice(2).split('=');
    if (value !== undefined) {
      args[key] = value;
    } else {
      const next = argv[i + 1];
      if (next && !next.startsWith('--')) {
        args[key] = next;
        i++;
      } else {
        args[key] = true;
      }
    }
  }
  return args;
}

function fetchJson(url) {
  return new Promise((resolve, reject) => {
    https
      .get(
        url,
        {
          headers: {
            'User-Agent': 'dm-note-webview2-fixed-runtime-downloader',
            Accept: 'application/json',
          },
        },
        (res) => {
          if (res.statusCode !== 200) {
            reject(new Error(`GET ${url} failed: ${res.statusCode}`));
            res.resume();
            return;
          }
          let data = '';
          res.setEncoding('utf8');
          res.on('data', (chunk) => (data += chunk));
          res.on('end', () => {
            try {
              resolve(JSON.parse(data));
            } catch (err) {
              reject(err);
            }
          });
        },
      )
      .on('error', reject);
  });
}

function downloadFile(url, outPath) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(outPath);
    https
      .get(
        url,
        {
          headers: {
            'User-Agent': 'dm-note-webview2-fixed-runtime-downloader',
            Accept: '*/*',
          },
        },
        (res) => {
          if (res.statusCode !== 200) {
            reject(new Error(`GET ${url} failed: ${res.statusCode}`));
            res.resume();
            return;
          }
          res.pipe(file);
          file.on('finish', () => file.close(resolve));
        },
      )
      .on('error', (err) => {
        fs.rmSync(outPath, { force: true });
        reject(err);
      });
  });
}

function cmpVersion(a, b) {
  const pa = a.split('.').map((n) => Number(n));
  const pb = b.split('.').map((n) => Number(n));
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const da = pa[i] ?? 0;
    const db = pb[i] ?? 0;
    if (da !== db) return da - db;
  }
  return 0;
}

function mapArchToNuGetId(arch) {
  switch (arch) {
    case 'x64':
      return { id: 'webview2.runtime.x64', runtime: 'win-x64' };
    case 'x86':
      return { id: 'webview2.runtime.x86', runtime: 'win-x86' };
    case 'arm64':
      return {
        id: 'webview2.runtime.arm64',
        runtime: 'win-arm64',
      };
    default:
      throw new Error(`Unsupported arch: ${arch} (use x64|x86|arm64)`);
  }
}

function ensureEmptyDir(dir) {
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
}

function findParentsContainingFile(rootDir, filename) {
  const matches = [];
  const stack = [rootDir];
  while (stack.length) {
    const current = stack.pop();
    let entries;
    try {
      entries = fs.readdirSync(current, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const entry of entries) {
      const full = path.join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(full);
      } else if (
        entry.isFile() &&
        entry.name.toLowerCase() === filename.toLowerCase()
      ) {
        matches.push(current);
      }
    }
  }
  return matches;
}

async function main() {
  if (process.platform !== 'win32') {
    throw new Error(
      'This script is intended for Windows (WebView2 Fixed runtime).',
    );
  }

  const args = parseArgs(process.argv);
  const major = String(args.major ?? '143');
  const arch = String(args.arch ?? 'x64');
  const outDir = path.resolve(
    args.out ??
      path.join(__dirname, '..', 'src-tauri', 'webview2-fixed-runtime'),
  );
  const force = Boolean(args.force);
  const dryRun = Boolean(args['dry-run'] ?? args.dryRun);

  const { id, runtime } = mapArchToNuGetId(arch);

  const indexUrl = `https://api.nuget.org/v3-flatcontainer/${id}/index.json`;
  const index = await fetchJson(indexUrl);
  const versions = Array.isArray(index.versions) ? index.versions : [];

  const candidates = versions.filter((v) => v.startsWith(`${major}.`));
  if (candidates.length === 0) {
    throw new Error(`No versions found for major=${major} (${id})`);
  }

  const selected = candidates.sort(cmpVersion).pop();
  const nupkgUrl = `https://api.nuget.org/v3-flatcontainer/${id}/${selected}/${id}.${selected}.nupkg`;

  if (dryRun) {
    console.log(`[webview2] selected ${id}@${selected} (${arch})`);
    console.log(`[webview2] nupkg: ${nupkgUrl}`);
    return;
  }

  const exeAlreadyThere = fs.existsSync(
    path.join(outDir, 'msedgewebview2.exe'),
  );
  const versionFile = path.join(
    outDir,
    'impl-dm-note-webview2-fixed-runtime-version.txt',
  );
  const existingVersion = fs.existsSync(versionFile)
    ? String(fs.readFileSync(versionFile, 'utf8')).trim()
    : '';
  if (!force && exeAlreadyThere && existingVersion === selected) {
    console.log(`[webview2] already present: ${id}@${selected} (${arch})`);
    console.log(`[webview2] dir: ${outDir}`);
    return;
  }

  const tmpRoot = fs.mkdtempSync(
    path.join(os.tmpdir(), 'impl-dm-note-webview2-'),
  );
  // Windows PowerShell's bundled Expand-Archive requires the .zip extension.
  const nupkgPath = path.join(tmpRoot, `${id}.${selected}.zip`);
  const extractDir = path.join(tmpRoot, 'extract');

  console.log(`[webview2] downloading ${id}@${selected}`);
  await downloadFile(nupkgUrl, nupkgPath);

  ensureEmptyDir(extractDir);
  execSync(
    `powershell -NoProfile -Command "Expand-Archive -LiteralPath '${nupkgPath}' -DestinationPath '${extractDir}' -Force -ErrorAction Stop"`,
    {
      stdio: 'inherit',
    },
  );

  let nativeDir = path.join(extractDir, 'runtimes', runtime, 'native');
  let exePath = path.join(nativeDir, 'msedgewebview2.exe');
  if (!fs.existsSync(exePath)) {
    const found = findParentsContainingFile(extractDir, 'msedgewebview2.exe');
    if (found.length !== 1) {
      throw new Error(
        `Unexpected package layout: expected 1 msedgewebview2.exe, found ${found.length}.`,
      );
    }
    nativeDir = found[0];
    exePath = path.join(nativeDir, 'msedgewebview2.exe');
  }

  ensureEmptyDir(outDir);
  fs.cpSync(nativeDir, outDir, { recursive: true });
  fs.writeFileSync(versionFile, `${selected}\n`, 'utf8');

  console.log(`[webview2] extracted to: ${outDir}`);
  console.log(
    `[webview2] set runtime via auto-detect (folder contains msedgewebview2.exe)`,
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
