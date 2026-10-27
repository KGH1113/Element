import fs from 'node:fs';
import path from 'node:path';
const [root, target] = process.argv.slice(2);
const settings = path.join(root, 'ImplResourcePack/Presentation/Settings');
const files = fs.readdirSync(settings).filter(name => name.endsWith('.cs') && name !== 'UIInteractionState.cs').map(name => path.join(settings, name));
const configuration = path.join(root, 'ImplResourcePack/Configuration');
for (const name of fs.readdirSync(configuration, { recursive: true }).filter(name => name.endsWith('.cs')))
  files.push(path.join(configuration, name));
files.push(path.join(root, 'ImplResourcePack/Presentation/Overlay/Shared/OverlayTheme.cs'));
files.push(path.join(root, 'ImplResourcePack/Presentation/Overlay/Shared/OverlayTextFactory.cs'));
files.push(path.join(root, 'ImplResourcePack/Presentation/Overlay/Shared/OverlayLocalization.cs'));
files.push(path.join(root, 'ImplResourcePack/Domain/DmNote/NativeViewerStatus.cs'));
files.push(path.join(root, 'ImplResourcePack/Domain/Input/ChatterFilter.cs'));
files.push(path.join(root, 'ImplResourcePack/Domain/DmNote/NativeViewerSurface.cs'));
files.push(path.join(root, 'ImplResourcePack/Infrastructure/Game/DmNote/NativeViewerHost.cs'));
files.push(path.join(root, 'ImplResourcePack/Infrastructure/Game/DmNote/NativeViewerMock.cs'));
files.push(path.join(root, 'ImplResourcePack/Infrastructure/Game/DmNote/NativeViewerLayoutTarget.cs'));
files.push(path.join(root, 'ImplResourcePack/Infrastructure/Game/Tweaks/PlanetPreviewRenderer.cs'));
for (const name of fs.readdirSync(path.join(root, 'ImplResourcePack/Presentation/Layout')).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'ImplResourcePack/Presentation/Layout', name));
for (const name of fs.readdirSync(path.join(root, 'ImplResourcePack/Domain/Layout')).filter(name => name.endsWith('.cs')))
  files.push(path.join(root, 'ImplResourcePack/Domain/Layout', name));
for (const source of files) {
  let code = fs.readFileSync(source, 'utf8');
  if (/^namespace [\w.]+;$/m.test(code)) code = code.replace(/^namespace ([\w.]+);$/m, 'namespace $1\n{') + '\n}\n';
  fs.writeFileSync(path.join(target, path.basename(source)), code);
}
fs.copyFileSync(path.join(root, 'scripts/ui-preview/SettingsPreview.cs'), path.join(target, 'Editor/SettingsPreview.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/OverlayTargetChecks.cs'), path.join(target, 'Editor/OverlayTargetChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/NativeLayoutChecks.cs'), path.join(target, 'Editor/NativeLayoutChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/PlanetPreviewChecks.cs'), path.join(target, 'Editor/PlanetPreviewChecks.cs'));
fs.copyFileSync(path.join(root, 'scripts/ui-preview/GameStubs.cs'), path.join(target, 'GameStubs.cs'));
fs.writeFileSync(path.join(target, 'PreviewAssembly.cs'), '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]\n');
