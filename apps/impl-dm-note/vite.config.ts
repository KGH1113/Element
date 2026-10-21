import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import svgr from "vite-plugin-svgr";
import path from "path";
import { visualizer } from "rollup-plugin-visualizer";
import analyzer from "rollup-plugin-analyzer";
import removeConsole from "./vite-plugin-remove-console.js";
import pkg from "./package.json";

export default defineConfig(() => {
  const projectRoot = __dirname;
  const repositoryRoot = path.resolve(projectRoot, "../..");
  const rendererRoot = path.resolve(projectRoot, "src/renderer");
  const windowsRoot = path.resolve(rendererRoot, "windows");
  const isAnalyze = process.env.ANALYZE === "true";

  return {
    // Vite 개발 서버 루트: /main/index.html, /overlay/index.html 경로로 접근 가능
    root: windowsRoot,
    base: "./",
    define: {
      __APP_VERSION__: JSON.stringify(pkg.version),
    },
    // Keep the bridge transform active in development and native fixtures too.
    optimizeDeps: { exclude: ["@tauri-apps/api"] },
    plugins: [
      {
        name: "dmnote-render-tauri-bridge",
        enforce: "pre",
        transform(code, id) {
          if (!id.replaceAll("\\", "/").includes("/@tauri-apps/api/")) return;
          // Tauri's native globals and methods are nonconfigurable. Keep them
          // intact; only API calls in the isolated render window use the snapshot.
          return code
            .replaceAll("window.__TAURI_INTERNALS__", "(window.__dmnRenderInternals ?? window.__TAURI_INTERNALS__)")
            .replaceAll("window.__TAURI_EVENT_PLUGIN_INTERNALS__", "(window.__dmnRenderEventInternals ?? window.__TAURI_EVENT_PLUGIN_INTERNALS__)");
        },
      },
      react({
        babel: {
          plugins: [
            ["babel-plugin-react-compiler", {
              // signals 파일 제외
              sources: (filename: string) => {
                if (filename.includes('stores/signals/')) return false;
                return true;
              },
            }],
          ],
        },
      }),
      svgr({
        include: "**/*.svg",
        svgrOptions: {
          // named export: { ReactComponent }
          exportType: "default",
        },
      }),
      removeConsole(),
      isAnalyze &&
        analyzer({
          summaryOnly: true,
        }),
      isAnalyze &&
        visualizer({
          filename: path.resolve(projectRoot, "dist", "stats.html"),
          template: "treemap",
          gzipSize: true,
          brotliSize: true,
          open: false,
        }),
    ].filter(Boolean),
    server: {
      port: 3400,
      strictPort: true,
      open: false,
      fs: {
        // 루트 상위(src/renderer 등) 경로 import 허용
        allow: [repositoryRoot, projectRoot, rendererRoot, windowsRoot],
      },
    },
    resolve: {
      alias: {
        "adofai-ipc": path.resolve(__dirname, "vendor/adofai-ipc/src"),
        "@components": path.resolve(rendererRoot, "components"),
        "@styles": path.resolve(rendererRoot, "styles"),
        "@windows": path.resolve(rendererRoot, "windows"),
        "@hooks": path.resolve(rendererRoot, "hooks"),
        "@api": path.resolve(rendererRoot, "api"),
        "@assets": path.resolve(rendererRoot, "assets"),
        "@utils": path.resolve(rendererRoot, "utils"),
        "@stores": path.resolve(rendererRoot, "stores"),
        "@constants": path.resolve(rendererRoot, "constants"),
        "@contexts": path.resolve(rendererRoot, "contexts"),
        "@plugins": path.resolve(rendererRoot, "plugins"),
        "@config": path.resolve(rendererRoot, "config"),
        "@shared": path.resolve(projectRoot, "src/types"),
        "@src": path.resolve(projectRoot, "src/"),
      },
      extensions: [".mjs", ".js", ".ts", ".jsx", ".tsx", ".json"],
    },
    build: {
      outDir: path.resolve(projectRoot, "dist/renderer"),
      emptyOutDir: true,
      rollupOptions: {
        input: {
          main: path.resolve(windowsRoot, "main/index.html"),
          overlay: path.resolve(windowsRoot, "overlay/index.html"),
          obs: path.resolve(windowsRoot, "obs/index.html"),
          render: path.resolve(windowsRoot, "render/index.html"),
        },
      },
    },
  };
});
