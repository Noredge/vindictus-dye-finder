import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: process.env.WEB_BASE || './',
  build: { sourcemap: false },
  server: { strictPort: true, port: 5173 },
  preview: { strictPort: true, port: 4173 },
});
