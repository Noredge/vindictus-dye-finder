import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readFileSync, writeFileSync } from 'node:fs';

const app = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8'));
const sourceCommit = process.env.WEB_COMMIT || process.env.GITHUB_SHA || null;
if (sourceCommit && !/^[a-f0-9]{40}$/.test(sourceCommit)) throw new Error('Invalid build commit.');
const sourceBranch = process.env.WEB_BRANCH || process.env.GITHUB_REF_NAME || null;

export default defineConfig({
  plugins: [react(), {
    name: 'build-identity',
    writeBundle(options) {
      writeFileSync(`${options.dir}/version.json`, JSON.stringify({ version: app.version, sourceCommit, sourceBranch }));
    },
  }],
  base: process.env.WEB_BASE || './',
  build: { sourcemap: false },
  server: { strictPort: true, port: 5173 },
  preview: { strictPort: true, port: 4173 },
});
