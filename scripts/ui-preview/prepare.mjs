import fs from 'node:fs';
import path from 'node:path';
const [root, target] = process.argv.slice(2);
const settings = path.join(root, 'Element/Presentation/Settings');
const files = fs.readdirSync(settings, { recursive: true }).filter(name => name.endsWith('.cs')).map(name => path.join(settings, name));
const configuration = path.join(root, 'Element/Configuration');
for (const name of fs.readdirSync(configuration, { recursive: true }).filter(name => name.endsWith('.cs')))
  files.push(path.join(configuration, name));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/OverlayTheme.cs'));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/OverlayTextFactory.cs'));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/OverlayLocalization.cs'));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/OverlayFonts.cs'));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/OverlayMaterialFactory.cs'));
files.push(path.join(root, 'Element/Infrastructure/Storage/OverlayFontCatalog.cs'));
files.push(path.join(root, 'Element/Infrastructure/Storage/OverlayFontSampling.cs'));
for (const name of ['IFontFilePicker', 'FontFilePickers', 'MacFontFilePicker', 'WindowsFontFilePicker'])
  files.push(path.join(root, 'Element/Infrastructure/Platform', name + '.cs'));
files.push(path.join(root, 'Element/Infrastructure/Storage/ProfileFileStore.cs'));
files.push(path.join(root, 'Element/Presentation/Overlay/Shared/NumberText.cs'));
files.push(path.join(root, 'Element/Domain/Input/ChatterFilter.cs'));
files.push(path.join(root, 'Element/Domain/Judgement/JudgementColors.cs'));
files.push(path.join(root, 'Element/Infrastructure/Mods/ElementClasses.cs'));
// Ports and use cases the settings UI, the layout editor and the overlay use.
for (const folder of ['Element/Application/Updates', 'Element/Application/Files', 'Element/Application/Profiles', 'Element/Application/Settings', 'Element/Application/Effects', 'Element/Application/Input', 'Element/Application/Fonts', 'Element/Application/Layout', 'Element/Application/KeyViewer', 'Element/Application/Mods'])
  for (const name of fs.readdirSync(path.join(root, folder)).filter(name => name.endsWith('.cs')))
    files.push(path.join(root, folder, name));
files.push(path.join(root, 'Element/Domain/Rendering/RenderViewerPlacement.cs'));
for (const name of fs.readdirSync(path.join(root, 'Element/Domain/KeyViewer')).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'Element/Domain/KeyViewer', name));
files.push(path.join(root, 'Element/Infrastructure/ElementNote/Viewers/INativeViewerLayoutHost.cs'));
files.push(path.join(root, 'Element/Infrastructure/ElementNote/Viewers/INativeViewerHost.cs'));
files.push(path.join(root, 'Element/Infrastructure/ElementNote/Viewers/NativeViewerLayoutTarget.cs'));
files.push(path.join(root, 'Element/Infrastructure/Game/Planets/PlanetPreviewRenderer.cs'));
files.push(path.join(root, 'Element/Domain/Tweaks/PlanetColorRule.cs'));
for (const name of fs.readdirSync(path.join(root, 'Element/Presentation/Layout'), { recursive: true }).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'Element/Presentation/Layout', name));
for (const name of fs.readdirSync(path.join(root, 'Element/Presentation/Ui'), { recursive: true }).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'Element/Presentation/Ui', name));
for (const name of fs.readdirSync(path.join(root, 'Element/Domain/Overlay'), { recursive: true }).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'Element/Domain/Overlay', name));
for (const folder of ['Element/Domain/Layout', 'Element/Presentation/Overlay/Canvas', 'Element/Presentation/Overlay/Migration', 'Element/Presentation/Overlay/Elements', 'Element/Presentation/Overlay/Mods'])
  for (const name of fs.readdirSync(path.join(root, folder)).filter(name => name.endsWith('.cs')))
    files.push(path.join(root, folder, name));
// ElementKit, minus Kit.cs: the preview attaches values through KitRegistry and
// lists its element's mod itself, without UMM.
for (const name of fs.readdirSync(path.join(root, 'ElementKit'), { recursive: true }))
  if (name.endsWith('.cs') && name !== 'Kit.cs' && !/^(bin|obj)[\\/]/.test(name))
    files.push(path.join(root, 'ElementKit', name));
for (const source of files) {
  let code = fs.readFileSync(source, 'utf8');
  if (/^namespace [\w.]+;$/m.test(code)) code = code.replace(/^namespace ([\w.]+);$/m, 'namespace $1\n{') + '\n}\n';
  fs.writeFileSync(path.join(target, path.basename(source)), code);
}
fs.copyFileSync(path.join(root, 'scripts/ui-preview/SettingsPreview.cs'), path.join(target, 'Editor/SettingsPreview.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/GradientChecks.cs'), path.join(target, 'Editor/GradientChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/ModElementChecks.cs'), path.join(target, 'Editor/ModElementChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/OverlayTargetChecks.cs'), path.join(target, 'Editor/OverlayTargetChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/NativeLayoutChecks.cs'), path.join(target, 'Editor/NativeLayoutChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/PlanetPreviewChecks.cs'), path.join(target, 'Editor/PlanetPreviewChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/GameStubs.cs'), path.join(target, 'GameStubs.cs'));
fs.writeFileSync(path.join(target, 'PreviewAssembly.cs'), '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]\n');
