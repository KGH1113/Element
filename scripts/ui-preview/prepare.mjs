import fs from 'node:fs';
import path from 'node:path';

// Copies the sources the UI preview compiles into the Unity fixture, flat:
// every file name is unique across these folders.
const [root, target] = process.argv.slice(2);

// Whole folders: every .cs under each, its subfolders too.
const folders = [
  'Element/Configuration',
  'Element/Domain/Overlay',
  'Element/Domain/Layout',
  'Element/Domain/KeyViewer',
  // Ports and use cases the settings UI, the layout editor and the overlay use.
  'Element/Application/Updates',
  'Element/Application/Files',
  'Element/Application/Profiles',
  'Element/Application/Settings',
  'Element/Application/Effects',
  'Element/Application/Input',
  'Element/Application/Fonts',
  'Element/Application/Layout',
  'Element/Application/KeyViewer',
  'Element/Application/Mods',
  'Element/Presentation/Ui',
  'Element/Presentation/Settings',
  'Element/Presentation/Layout',
  'Element/Presentation/Overlay/Canvas',
  'Element/Presentation/Overlay/Elements',
  'Element/Presentation/Overlay/Text',
  'Element/Presentation/Overlay/Shape',
  'Element/Presentation/Overlay/Mods',
  'Element/Presentation/Overlay/Fonts',
  'Element/Presentation/Overlay/Migration',
];

// Single files from folders whose other files need the running game, the OS
// or ports the preview has no use for.
const files = [
  'Element/Domain/Tweaks/ChatterFilter.cs',
  'Element/Domain/Tweaks/PlanetColorRule.cs',
  'Element/Domain/Judgement/JudgementColors.cs',
  'Element/Domain/Rendering/RenderViewerPlacement.cs',
  'Element/Application/Gameplay/IPlanetPreview.cs',
  // The values texts show; the value writers need the game.
  'Element/Presentation/Overlay/Values/NumberText.cs',
  'Element/Presentation/Overlay/Values/OverlayValues.cs',
  'Element/Presentation/Overlay/Values/OverlayLabels.cs',
  'Element/Presentation/Overlay/Values/OverlayLocalization.cs',
  'Element/Presentation/Notices/UpdateStatusText.cs',
  ...['OverlayFontCatalog', 'OverlayFontFolder', 'InstalledFontList', 'OverlayFontFiles', 'OverlayFontFaces', 'OverlayFontSampling', 'ProfileFileStore']
    .map(name => `Element/Infrastructure/Storage/${name}.cs`),
  ...['IFontFilePicker', 'FontFilePickers', 'MacFontFilePicker', 'WindowsFontFilePicker']
    .map(name => `Element/Infrastructure/Platform/${name}.cs`),
  'Element/Infrastructure/Mods/ElementClasses.cs',
  ...['INativeViewerLayoutHost', 'INativeViewerHost', 'NativeViewerLayoutTarget', 'NativeViewerSamplePreview']
    .map(name => `Element/Infrastructure/ElementNote/Viewers/${name}.cs`),
  ...['PlanetPreviewRenderer', 'PlanetPreviewSource', 'PlanetPreviewSources', 'PlanetPreviewVisual', 'PlanetPreviewObjects']
    .map(name => `Element/Infrastructure/Game/Planets/${name}.cs`),
];

const sources = files.map(file => path.join(root, file));
for (const folder of folders)
  for (const name of fs.readdirSync(path.join(root, folder), { recursive: true }))
    if (name.endsWith('.cs'))
      sources.push(path.join(root, folder, name));
// ElementKit, minus Kit.cs: the preview attaches values through KitRegistry and
// lists its element's mod itself, without UMM.
for (const name of fs.readdirSync(path.join(root, 'ElementKit'), { recursive: true }))
  if (name.endsWith('.cs') && name !== 'Kit.cs' && !/^(bin|obj)[\\/]/.test(name))
    sources.push(path.join(root, 'ElementKit', name));

const seen = new Map();
for (const source of sources) {
  const name = path.basename(source);
  if (seen.has(name))
    throw new Error(`Two sources named ${name}: ${seen.get(name)} and ${source}`);
  seen.set(name, source);
  let code = fs.readFileSync(source, 'utf8');
  if (/^namespace [\w.]+;$/m.test(code)) code = code.replace(/^namespace ([\w.]+);$/m, 'namespace $1\n{') + '\n}\n';
  fs.writeFileSync(path.join(target, name), code);
}
// Every check file; GameStubs goes with the runtime sources.
for (const check of fs.readdirSync(path.join(root, 'scripts/ui-preview')))
  if (check.endsWith('.cs') && check !== 'GameStubs.cs')
    fs.copyFileSync(path.join(root, 'scripts/ui-preview', check), path.join(target, 'Editor', check));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/GameStubs.cs'), path.join(target, 'GameStubs.cs'));
fs.writeFileSync(path.join(target, 'PreviewAssembly.cs'), '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]\n');
// ElementUI, which Element's screens are built with: the same pinned DLLs Element ships.
fs.mkdirSync(path.join(target, 'Plugins'), { recursive: true });
for (const dll of ['ElementUI.Core.dll', 'ElementUI.UGUI.dll'])
  fs.copyFileSync(path.join(root, 'vendor/elementui', dll), path.join(target, 'Plugins', dll));
