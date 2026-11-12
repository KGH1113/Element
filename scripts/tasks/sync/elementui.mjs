import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// Pins ElementUI: the runtime pair Element ships (Core, UGUI) and the build-time
// analyzer, copied from a clean, committed checkout of the ElementUI repository.
const root = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const upstream = process.argv[2] && path.resolve(process.argv[2]);
if (!upstream || upstream === root)
  throw new Error('Specify the local ElementUI repository: ./scripts/run.sh elementui-sync ~/dev/src/ElementUI');
const git = (...args) => execFileSync('git', ['-C', upstream, ...args], { encoding: 'utf8' }).trim();
if (git('status', '--porcelain')) throw new Error('Commit ElementUI changes before syncing it.');
const revision = git('rev-parse', 'HEAD');
const build = project => execFileSync('dotnet', ['build', path.join(upstream, project), '-c', 'Release', '-v', 'quiet', '-nologo'], { stdio: 'inherit' });
build('src/ElementUI.UGUI/ElementUI.UGUI.csproj');
build('src/ElementUI.Analyzers/ElementUI.Analyzers.csproj');
const vendor = path.join(root, 'vendor/elementui');
const files = {
  'ElementUI.Core.dll': 'src/ElementUI.Core/bin/Release/ElementUI.Core.dll',
  'ElementUI.Core.xml': 'src/ElementUI.Core/bin/Release/ElementUI.Core.xml',
  'ElementUI.UGUI.dll': 'src/ElementUI.UGUI/bin/Release/ElementUI.UGUI.dll',
  'analyzers/ElementUI.Analyzers.dll': 'src/ElementUI.Analyzers/bin/Release/ElementUI.Analyzers.dll',
  LICENSE: 'LICENSE',
};
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const sums = [];
for (const [name, source] of Object.entries(files)) {
  const target = path.join(vendor, name);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(path.join(upstream, source), target);
  sums.push(`${hash(target)}  ${name}`);
}
fs.writeFileSync(path.join(vendor, 'SHA256SUMS'), sums.join('\n') + '\n');
fs.writeFileSync(
  path.join(vendor, 'README.md'),
  `# ElementUI\n\nThe declarative UI library Element's screens are built with. Pinned from ElementUI \`${revision}\` by \`./scripts/run.sh elementui-sync <repository>\`; don't edit these files by hand.\n\n- \`ElementUI.Core.dll\`, \`ElementUI.UGUI.dll\`: shipped next to Element.dll in each version folder; other mods bind to them through Element.\n- \`analyzers/ElementUI.Analyzers.dll\`: build-time rules (UI001…), not shipped.\n`
);
console.log(`ElementUI ${revision.slice(0, 7)} pinned in vendor/elementui`);
