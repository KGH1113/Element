import assert from 'node:assert/strict';
import { readFile, writeFile, mkdtemp, mkdir, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { runInNewContext } from 'node:vm';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { build, preview } from 'vite';
import { mapEvents, frameAt } from '../src/renderer/render/timing.mjs';

if (process.platform !== 'darwin')
  throw new Error(
    'The native WKWebView fixture requires macOS. Verify the WebView2 capture on Windows separately.',
  );
const app = resolve(import.meta.dirname, '..');
const runtimeRequire = createRequire(join(app, 'package.json'));
const { code } = await runtimeRequire('esbuild').transform(
  await readFile(join(app, 'src/renderer/defaults.ts'), 'utf8'),
  { loader: 'ts', format: 'cjs' },
);
const module = { exports: {} };
runInNewContext(code, { module, exports: module.exports });
const settings = module.exports.getDefaultSettingsState();
settings.noteEffect = true;
settings.keyCounterEnabled = true;
settings.useCustomCSS = true;
settings.customCSS = {
  path: null,
  content: '* { font-family: monospace !important; }',
};
settings.fontSettings = { customFonts: [] };
settings.useCustomJS = true;
settings.customJS = {
  path: null,
  content: '',
  plugins: [
    {
      id: 'render-fixture',
      name: 'render-fixture.js',
      enabled: true,
      content: `// @id render-fixture
window.api.plugin.storage.set('fixture-written', 42);
window.api.plugin.defineElement({
  name: 'Replay fixture', previewState: { count: 0 },
  template: state => '<div style="font:16px monospace;color:white;background:#126">Recorded '+state.count+'</div>',
  onMount: context => {
    let count = 0;
    context.onHook('rawKey', input => { if (input.state === 'DOWN') context.setState({ count: ++count }); });
  }
});`,
    },
  ],
};
const pluginElements = [
  {
    id: 'panel',
    fullId: 'render-fixture::panel',
    pluginId: 'render-fixture',
    definitionId: 'render-fixture',
    tabId: 'hand-export',
    html: '<!-- plugin-element -->',
    position: { x: 0, y: 0 },
    measuredSize: { width: 120, height: 22 },
    settings: {},
    state: { count: 0 },
    draggable: false,
  },
];
const stored = JSON.parse(
  await readFile(join(app, 'src-tauri/default_positions.json'), 'utf8'),
);
const first = Object.values(stored).find((positions) => positions.length)?.[0];
const positions = [0, 90, 180].map((dx) => ({
  ...first,
  dx,
  dy: 170,
  width: 60,
  height: 60,
}));
const snapshot = {
  settings,
  defaults: {
    settings,
    counterSettings: module.exports.getDefaultCounterSettings(),
  },
  keys: {
    'hand-export': ['A', 'S', 'MOUSE1'],
    'foot-export': ['A', 'S', 'MOUSE1'],
  },
  positions: { 'hand-export': positions, 'foot-export': positions },
  selectedViewerTabs: { hand: 'hand-export', foot: 'foot-export' },
  selectedKeyType: 'hand-export',
  currentMode: 'hand-export',
  tabs: [
    { id: 'hand-export', name: 'Hands', viewerKind: 'hand' },
    { id: 'foot-export', name: 'Feet', viewerKind: 'foot' },
  ],
  overlay: { visible: true, locked: true, anchor: 'top-left' },
  keyCounters: {},
  layerGroups: {},
  tabNoteOverrides: {},
  tabCssOverrides: {},
};
const manifest = {
  snapshot,
  viewerKind: 'hand',
  fpsNumerator: 60,
  fpsDenominator: 1,
  frameCount: 40,
  timeline: [{ outputTimeUs: 0, replayTimeUs: 0, rate: 2 }],
};
const events = mapEvents(manifest, [
  { timeUs: 10000, key: 'A', down: true, sequence: 0 },
  { timeUs: 20000, key: 'A', down: false, sequence: 1 },
  { timeUs: 200000, key: 'S', down: true, sequence: 2 },
  { timeUs: 300000, key: 'Mouse0', down: true, sequence: 3 },
  { timeUs: 500000, key: 'Mouse0', down: false, sequence: 4 },
  { timeUs: 600000, key: 'S', down: false, sequence: 5 },
]);
// App receives Unity symbols and maps the preset's platform-native labels itself.
const wire = events.map((event) => ({
  ...event,
  key: event.key === 'MOUSE1' ? 'Mouse0' : event.key,
}));
let cursor = 0;
const frames = Array.from({ length: 40 }, (_, frameIndex) => {
  const frame = frameAt(manifest, frameIndex),
    due = [];
  while (
    cursor < wire.length &&
    wire[cursor].outputTimeUs <= frame.outputTimeUs
  )
    due.push(wire[cursor++]);
  return { ...frame, events: due };
});
const directory = await mkdtemp(join(tmpdir(), 'impl-dmnote-native-render-'));
async function execute(command, args) {
  await new Promise((done, reject) => {
    const child = spawn(command, args, { stdio: ['ignore', 'ignore', 'pipe'] });
    let errors = '';
    child.stderr.on('data', (data) => (errors = (errors + data).slice(-8192)));
    child.on('error', reject);
    child.on('close', (code) =>
      code === 0 ? done() : reject(new Error(`${command}: ${errors}`)),
    );
  });
}
let server;
try {
  await build({ configFile: join(app, 'vite.config.ts'), logLevel: 'error' });
  server = await preview({
    configFile: join(app, 'vite.config.ts'),
    preview: { port: 0, strictPort: false, host: '127.0.0.1' },
  });
  const port = server.httpServer.address().port;
  const executable = join(directory, 'fixture');
  await execute('xcrun', [
    'swiftc',
    join(app, 'scripts/render-native-fixture.swift'),
    '-framework',
    'AppKit',
    '-framework',
    'WebKit',
    '-o',
    executable,
    '-module-cache-path',
    join(directory, 'swift-cache'),
  ]);
  const hashes = [];
  for (let pass = 0; pass < 2; pass++) {
    const output = join(directory, String(pass));
    await mkdir(output);
    await writeFile(
      join(output, 'config.json'),
      JSON.stringify({
        snapshot,
        viewerKind: 'hand',
        initialOutputTimeUs: -200000,
        pluginStorage: {},
        pluginElements,
        assetUrls: {},
        verify: true,
      }),
    );
    await writeFile(join(output, 'frames.json'), JSON.stringify(frames));
    await execute(executable, [
      output,
      `http://127.0.0.1:${port}/render/index.html`,
    ]);
    hashes.push(
      await Promise.all(
        frames.map(async (frame) =>
          createHash('sha256')
            .update(
              await readFile(join(output, `frame-${frame.frameIndex}.png`)),
            )
            .digest('hex'),
        ),
      ),
    );
    const reports = JSON.parse(
      await readFile(join(output, 'reports.json'), 'utf8'),
    );
    assert.equal(reports[1].counters['hand-export'].A, 1);
    assert(!reports[1].activeKeys.includes('A'));
    assert(reports[8].activeKeys.includes('S'));
    assert(!reports[19].activeKeys.includes('S'));
    assert(reports[10].activeKeys.includes('MOUSE1'));
    assert(!reports[16].activeKeys.includes('MOUSE1'));
    assert.equal(reports[16].counters['hand-export'].MOUSE1, 1);
    assert(
      Object.values(reports[0].pluginStorage).includes(42),
      'Custom JS must execute against isolated plugin storage.',
    );
    assert.equal(
      reports[16].pluginElements[0].state.count,
      3,
      'A captured plugin panel must receive only the recorded raw inputs and update at the virtual frame barrier.',
    );
  }
  assert.deepEqual(
    hashes[0],
    hashes[1],
    'Identical native render jobs must produce identical pixels.',
  );
  const decoded = spawnSync(
    process.env.FFMPEG_PATH || 'ffmpeg',
    [
      '-v',
      'error',
      '-i',
      join(directory, '0/frame-1.png'),
      '-frames:v',
      '1',
      '-f',
      'rawvideo',
      '-pix_fmt',
      'rgba',
      'pipe:1',
    ],
    { maxBuffer: 32 * 1024 * 1024 },
  );
  assert.equal(decoded.status, 0, String(decoded.stderr));
  let transparent = 0,
    opaque = 0;
  for (let i = 3; i < decoded.stdout.length; i += 4) {
    if (decoded.stdout[i] === 0) transparent++;
    if (decoded.stdout[i] > 200) opaque++;
  }
  assert(
    transparent > 10000 && opaque > 100,
    'Native capture must preserve transparent and visible overlay pixels.',
  );
  console.log(
    'PASS: hidden native WKWebView, full overlay, alpha, exact short taps/holds/mouse, isolated custom JS storage, identical pixels across sessions.',
  );
} finally {
  await new Promise((done) => server ? server.httpServer.close(done) : done());
  await rm(directory, { recursive: true, force: true });
}
