const fs = require("node:fs");
const path = require("node:path");

const repoRoot = path.resolve(__dirname, "..");
const appRoot = path.join(repoRoot, "apps", "element-note");
const ignoredDirectories = new Set([
  ".git",
  "Library",
  "node_modules",
  "target",
  "dist",
  "build",
  "Release",
]);

function walk(directory) {
  const files = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (ignoredDirectories.has(entry.name)) continue;
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...walk(entryPath));
    else files.push(entryPath);
  }
  return files;
}

const failures = [];
for (const removedPath of [
  "GameBarOverlay",
  "docs",
  "src/renderer/assets/mp4",
  "scripts/build-single-exe-win.js",
]) {
  if (fs.existsSync(path.join(appRoot, removedPath)))
    failures.push(`removed path still exists: ${removedPath}`);
}

const textFiles = walk(appRoot).filter((file) => {
  const extension = path.extname(file).toLowerCase();
  return [
    ".cjs",
    ".css",
    ".html",
    ".json",
    ".md",
    ".mdx",
    ".mjs",
    ".rs",
    ".toml",
    ".ts",
    ".tsx",
    ".yml",
    ".yaml",
  ].includes(extension);
});

const forbidden = [
  [/github\.com\/lee-sihun\/DmNote\/releases/i, "upstream release URL"],
  [/CN=esihunc/i, "upstream certificate identity"],
  [/com\.dmnote\.desktop/i, "upstream application identifier"],
  [
    /app_auto_update|autoUpdateEnabled|UpdateModal|useUpdateStore/i,
    "removed updater API",
  ],
  [/docs\/assets|assets\/mp4/i, "removed promotional media reference"],
];

for (const file of textFiles) {
  if (file.endsWith("THIRD_PARTY_LICENSES.txt")) continue;
  const content = fs.readFileSync(file, "utf8");
  for (const [pattern, label] of forbidden) {
    if (pattern.test(content))
      failures.push(`${label}: ${path.relative(repoRoot, file)}`);
  }
}

for (const file of walk(repoRoot).filter((candidate) =>
  /\.(?:md|mdx)$/i.test(candidate),
)) {
  if (file.endsWith("THIRD_PARTY_LICENSES.txt")) continue;
  const content = fs.readFileSync(file, "utf8");
  for (const match of content.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
    let target = match[1].trim().replace(/^<|>$/g, "").split(/[?#]/, 1)[0];
    if (!target || target.startsWith("/") || /^(?:[a-z]+:|#)/i.test(target))
      continue;
    target = decodeURIComponent(target);
    if (!fs.existsSync(path.resolve(path.dirname(file), target))) {
      failures.push(
        `broken Markdown link in ${path.relative(repoRoot, file)}: ${match[1]}`,
      );
    }
  }
}

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Public-release path, string, and Markdown-link audit passed.");
