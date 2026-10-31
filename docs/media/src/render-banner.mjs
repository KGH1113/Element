// Renders banner.html to docs/media/banner.png at 2x.
// The version readouts come from Element/Info.json, so rerun this after a version bump.
// Usage: node docs/media/src/render-banner.mjs <path-to-playwright-module>
import { readFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const info = JSON.parse(readFileSync(path.join(here, "../../../Element/Info.json"), "utf8"));
const trimPatch = (v) => String(v).replace(/\.0$/, "");

const { chromium } = await import(pathToFileURL(path.resolve(process.argv[2])).href);
const browser = await chromium.launch({ channel: "chrome" });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 }, deviceScaleFactor: 2 });
await page.goto(pathToFileURL(path.join(here, "banner.html")).href);
await page.evaluate(
  ({ version, umm }) => {
    document.getElementById("version").textContent = version;
    document.getElementById("umm").textContent = umm;
  },
  { version: info.Version, umm: trimPatch(info.ManagerVersion) },
);
await page.evaluate(() => document.fonts.ready);
await page.screenshot({ path: path.join(here, "..", "banner.png") });
await browser.close();
console.log(`banner.png: v${info.Version}, UnityModManager ${trimPatch(info.ManagerVersion)}+`);
