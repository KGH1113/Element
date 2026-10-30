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
const tauriConfig = JSON.parse(await readFile(join(app, 'src-tauri/tauri.conf.json'), 'utf8'));
settings.noteEffect = true;
// Exercise fractional note-expiry deadlines within the captured frame range.
settings.noteSettings.speed = 1234;
settings.noteSettings.trackHeight = 123;
settings.keyCounterEnabled = true;
settings.useCustomCSS = true;
settings.customCSS = {
  path: null,
  content: '[data-state] { --key-radius: 17px; --key-bg: rgb(16, 32, 48); }',
};
const fontPath = join(app, 'src/renderer/assets/fonts/PretendardVariable.woff2');
const fontData = await readFile(fontPath);
settings.fontSettings = { customFonts: [{
  id: 'fixture-font', type: 'local', name: 'Fixture Font',
  displayName: 'Fixture Font', enabled: true, localPath: fontPath,
}] };
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
  fontFamily: 'Fixture Font',
}));
const snapshot = {
  settings,
  defaults: {
    settings,
    counterSettings: module.exports.getDefaultCounterSettings(),
  },
  keys: {
    'hand-export': ['A', 'S', 'MOUSE1'],
    'foot-export': ['S', 'MOUSE1'],
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
  tabCssOverrides: {
    'foot-export': {
      enabled: true, path: 'fixture-foot.css',
      content: '[data-state] { --key-radius: 23px; --key-bg: rgb(64, 32, 16); }',
    },
  },
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
const directory = await mkdtemp(join(tmpdir(), 'element-note-native-render-'));
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
    plugins: [{
      name: 'native-render-tauri-csp-fixture',
      configurePreviewServer(server) {
        server.middlewares.use(async (request, response, next) => {
          if (request.url !== '/render/index.html') return next();
          // Tauri adds a nonce to inline styles and to style-src. WebKit then
          // ignores unsafe-inline, so dynamic CSS/font styles need that nonce too.
          const nonce = 'native-render-fixture';
          let html = await readFile(join(app, 'dist/renderer/render/index.html'), 'utf8');
          const hasInlineStyle = /<style\b/.test(html);
          html = html.replaceAll('<style', `<style nonce="${nonce}"`);
          const csp = hasInlineStyle
            ? tauriConfig.app.security.csp.replace(/style-src[^;]*/, `$& 'nonce-${nonce}'`)
            : tauriConfig.app.security.csp;
          response.setHeader('Content-Type', 'text/html');
          response.setHeader('Content-Security-Policy', csp);
          response.end(html);
        });
      },
    }],
  });
  const port = server.httpServer.address().port;
  const executable = join(directory, 'fixture');
  await execute('xcrun', [
    'swiftc',
    '-O',
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
  const conversion = [], snapshots = [];
  for (let pass = 0; pass < 6; pass++) {
    const format = pass < 2 ? 'png' : 'rgba';
    const viewerKind = pass < 4 ? 'hand' : 'foot';
    const mode = `${viewerKind}-export`;
    const output = join(directory, String(pass));
    await mkdir(output);
    await writeFile(
      join(output, 'config.json'),
      JSON.stringify({
        snapshot,
        viewerKind,
        initialOutputTimeUs: -200000,
        pluginStorage: {},
        pluginElements: pluginElements.map((element) => ({ ...element, tabId: mode })),
        assetUrls: { [fontPath]: `data:font/woff2;base64,${fontData.toString('base64')}` },
        verify: true,
      }),
    );
    await writeFile(join(output, 'frames.json'), JSON.stringify(frames));
    await execute(executable, [
      output,
      `http://127.0.0.1:${port}/render/index.html`,
      format,
    ]);
    hashes.push(
      await Promise.all(
        frames.map(async (frame) =>
          createHash('sha256')
            .update(
              await readFile(join(output, `frame-${frame.frameIndex}.${format}`)),
            )
            .digest('hex'),
        ),
      ),
    );
    const reports = JSON.parse(
      await readFile(join(output, 'reports.json'), 'utf8'),
    );
    const css = viewerKind === 'foot' ? snapshot.tabCssOverrides[mode] : settings.customCSS;
    assert.equal(reports[0].styles.customCss, css.content, 'Render must apply the frozen global or selected-tab CSS.');
    assert.equal(reports[0].styles.borderRadius, viewerKind === 'foot' ? '23px' : '17px', 'Render must use custom CSS key variables under the packaged CSP.');
    assert.equal(reports[0].styles.backgroundColor, viewerKind === 'foot' ? 'rgb(64, 32, 16)' : 'rgb(16, 32, 48)', 'Render must use custom CSS colors.');
    assert(reports[0].styles.fontFamily.includes('Fixture Font'), 'Render must keep the selected key font.');
    assert(reports[0].styles.fonts.some(font => font.family === 'Fixture Font' && font.status === 'loaded'), 'Render must load the frozen local font before the first frame.');
    assert.equal(reports[0].styles.viewerSurface, true, 'Render must share the live viewer style boundary.');
    conversion.push(reports.reduce((sum, report) => sum + report.conversionMs, 0) / reports.length);
    snapshots.push(reports.reduce((sum, report) => sum + report.snapshotMs, 0) / reports.length);
    if (pass === 0 || pass === 2) console.log(`${format} source size: ${reports[0].sourceWidth}x${reports[0].sourceHeight}`);
    if (viewerKind === 'hand') assert.equal(reports[1].counters[mode].A, 1);
    else assert.equal(reports[1].counters[mode]?.A, undefined, 'Foot capture must use its own selected tab, not hand mappings.');
    assert(!reports[1].activeKeys.includes('A'));
    assert(reports[8].activeKeys.includes('S'));
    assert(!reports[19].activeKeys.includes('S'));
    assert(reports[10].activeKeys.includes('MOUSE1'));
    assert(!reports[16].activeKeys.includes('MOUSE1'));
    assert.equal(reports[16].counters[mode].MOUSE1, 1);
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
  assert.deepEqual(hashes[2], hashes[3], 'Raw pixel captures must also be deterministic.');
  assert.deepEqual(hashes[4], hashes[5], 'Foot captures must be deterministic across sessions.');
  assert.notDeepEqual(hashes[2], hashes[4], 'Distinct hand and foot presets must produce distinct pixels.');
  const baseline = spawnSync(process.env.FFMPEG_PATH || 'ffmpeg', [
    '-v', 'error', '-i', join(directory, '0/frame-16.png'), '-vf', 'scale=320:320',
    '-frames:v', '1', '-f', 'rawvideo', '-pix_fmt', 'rgba', 'pipe:1',
  ], { maxBuffer: 32 * 1024 * 1024 });
  assert.equal(baseline.status, 0, String(baseline.stderr));
  const raw = await readFile(join(directory, '2/frame-16.rgba'));
  assert.equal(raw.length, 320 * 320 * 4);
  let alphaDifference = 0, colorDifference = 0;
  let alphaTotal = 0, colorTotal = 0, colorCount = 0;
  for (let i = 0; i < raw.length; i += 4) {
    alphaDifference = Math.max(alphaDifference, Math.abs(raw[i + 3] - baseline.stdout[i + 3]));
    alphaTotal += Math.abs(raw[i + 3] - baseline.stdout[i + 3]);
    if (raw[i + 3] > 200 && baseline.stdout[i + 3] > 200)
      for (let channel = 0; channel < 3; channel++) {
        colorDifference = Math.max(colorDifference, Math.abs(raw[i + channel] - baseline.stdout[i + channel]));
        colorTotal += Math.abs(raw[i + channel] - baseline.stdout[i + channel]);
        colorCount++;
      }
  }
  console.log(`Pixel comparison: alpha mean=${alphaTotal / (raw.length / 4)}, color mean=${colorTotal / colorCount}, maxima=${alphaDifference}/${colorDifference}`);
  assert(alphaDifference <= 2 && colorDifference <= 3,
    `Raw capture must preserve orientation, colors and alpha: alpha=${alphaDifference}, color=${colorDifference}`);
  console.log(`Native capture averages: PNG conversion=${conversion[0].toFixed(2)}ms, raw conversion=${conversion[2].toFixed(2)}ms; snapshots=${snapshots[0].toFixed(2)}/${snapshots[2].toFixed(2)}ms.`);
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
    'PASS: hidden native WKWebView with packaged Tauri CSP, global/tab custom CSS, frozen local fonts, distinct hand/foot presets, full overlay, alpha, exact short taps/holds/mouse, isolated custom JS storage, identical pixels across sessions.',
  );
} finally {
  await new Promise((done) => server ? server.httpServer.close(done) : done());
  await rm(directory, { recursive: true, force: true });
}
