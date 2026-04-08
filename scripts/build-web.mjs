import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, "..");
const entryPoint = resolve(projectRoot, "src/Web/main.js");
const outfile = resolve(projectRoot, "src/Web/dist/app.bundle.js");

await mkdir(dirname(outfile), { recursive: true });

await build({
  entryPoints: [entryPoint],
  bundle: true,
  format: "iife",
  platform: "browser",
  target: "es2020",
  sourcemap: false,
  minify: false,
  outfile
});
