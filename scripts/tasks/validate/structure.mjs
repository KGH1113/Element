// Checks the rules in docs/plans/structure-plan.md §9 and
// docs/plans/data-flow-plan.md that a compiler can't:
// - layers only reference the layers inside them;
// - screen code (the overlay editor and the settings window) has one type
//   per file, named after it, and the name ends in its role;
// - a part never knows the screens that use it, and screens never know each other.
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[2] ?? '.');
const element = path.join(root, 'Element');
const problems = [];

function filesUnder(dir) {
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'bin' || entry.name === 'obj') continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...filesUnder(full));
    else if (entry.name.endsWith('.cs')) out.push(full);
  }
  return out;
}

const relative = file => path.relative(root, file).split(path.sep).join('/');
// Code only: comments and strings may name anything.
const code = text =>
  text
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/[^\n]*/g, '')
    .replace(/@?"(?:[^"\\\n]|\\.)*"/g, '""');

// ---- Layers ----

const inner = {
  Configuration: ['Domain', 'Application', 'Infrastructure', 'Presentation', 'Bootstrap'],
  Domain: ['Application', 'Infrastructure', 'Presentation', 'Bootstrap'],
  Application: ['Infrastructure', 'Presentation', 'Bootstrap'],
  Infrastructure: ['Presentation', 'Bootstrap'],
  Presentation: ['Infrastructure', 'Bootstrap'],
};
for (const [layer, forbidden] of Object.entries(inner)) {
  const pattern = new RegExp(`\\bElement\\.(${forbidden.join('|')})\\b`);
  for (const file of filesUnder(path.join(element, layer))) {
    const match = code(fs.readFileSync(file, 'utf8')).match(pattern);
    if (match) problems.push(`${relative(file)}: ${layer} must not know ${match[1]} (${match[0]})`);
  }
}

// ---- Screen code ----

const screens = ['Presentation/OverlayEditor', 'Presentation/Settings'];
// What a class derives from first says its role.
const roles = [
  { suffix: 'ViewModel', base: /^\w*ViewModel(<.*>)?$/, what: 'a view model' },
  { suffix: 'View', base: /^(View<.*>|ComponentView|NativeView)$/, what: 'a view' },
  { suffix: 'Graphic', base: /^(MaskableGraphic|UiVectorGraphic|Graphic)$/, what: 'a graphic' },
];
const declaration =
  /^(?:internal|public)\b[^\n]*?\b(class|struct|enum|interface|record)\s+(\w+)(?:<[^>\n]*>)?(?:\s*:\s*([^\n{]+))?/gm;

for (const area of screens) {
  for (const file of filesUnder(path.join(element, area))) {
    const text = code(fs.readFileSync(file, 'utf8'));
    const name = path.basename(file, '.cs');
    const types = [...text.matchAll(declaration)];
    if (types.length !== 1) problems.push(`${relative(file)}: ${types.length} types; one per file`);
    for (const [, kind, type, bases = ''] of types) {
      if (type !== name) problems.push(`${relative(file)}: holds ${type}; the file is named after its type`);
      if (kind !== 'class') continue;
      const firstBase = bases.split(',')[0].trim();
      for (const role of roles) {
        if (role.base.test(firstBase) && !type.endsWith(role.suffix))
          problems.push(`${relative(file)}: ${type} is ${role.what}; its name ends in ${role.suffix}`);
      }
      if (/Model$/.test(type) && !/ViewModel$/.test(type))
        problems.push(`${relative(file)}: ${type} ends in Model; a view model ends in ViewModel`);
    }
  }
}

// Shared parts never know the screens that use them. The full-screen editors
// never know each other or the canvas; the canvas opens them only through
// the kinds, the one place that knows each kind of element.
const editor = 'Element.Presentation.OverlayEditor';
const forbidden = {
  Parts: ['TextEditor', 'ShapeEditor', 'ModEditor', 'Canvas', 'Kinds', 'KeyViewers'],
  TextEditor: ['ShapeEditor', 'ModEditor', 'Canvas', 'Kinds'],
  ShapeEditor: ['TextEditor', 'ModEditor', 'Canvas', 'Kinds'],
  ModEditor: ['TextEditor', 'ShapeEditor', 'Canvas', 'Kinds'],
  Canvas: ['TextEditor', 'ShapeEditor', 'ModEditor'],
};
const usingOf = text => [...text.matchAll(/^using (?:static )?([\w.]+);/gm)].map(m => m[1]);
for (const file of filesUnder(path.join(element, 'Presentation/OverlayEditor'))) {
  const rel = relative(file);
  const own = rel.split('/')[3];
  const text = code(fs.readFileSync(file, 'utf8'));
  for (const folder of forbidden[own] ?? []) {
    const ns = `${editor}.${folder}`;
    if (usingOf(text).some(u => u === ns || u.startsWith(ns + '.')) || text.includes(ns + '.'))
      problems.push(`${rel}: ${own} knows ${folder}; ${own === 'Parts' ? 'parts never know the screens that use them' : 'screens never know each other'}`);
  }
}

if (problems.length > 0) {
  console.error(problems.join('\n'));
  console.error(`\n${problems.length} structure problem(s). The rules: docs/plans/data-flow-plan.md`);
  process.exit(1);
}
console.log('Structure: layers, roles and folders follow the rules.');
