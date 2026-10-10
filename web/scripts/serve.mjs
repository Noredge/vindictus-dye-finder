import { createServer } from 'node:http';
import { readFileSync, existsSync, statSync, writeFileSync, mkdirSync, unlinkSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve, relative, extname } from 'node:path';
import { randomBytes } from 'node:crypto';

const home = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const root = resolve(home, 'dist'), state = resolve(home, '.preview-state');
const port = Number(process.argv[2] || 4173), token = randomBytes(24).toString('hex');
if (!Number.isInteger(port) || port < 1024 || port > 65535 || !existsSync(resolve(root, 'index.html'))) throw new Error('Build the web app first and use a port from 1024 to 65535.');
const mime = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.md': 'text/plain; charset=utf-8', '.txt': 'text/plain; charset=utf-8' };
const server = createServer((request, response) => {
  response.setHeader('X-Content-Type-Options', 'nosniff');
  response.setHeader('Cache-Control', 'no-store');
  if (request.url === '/_preview/status') {
    response.setHeader('Content-Type', 'application/json'); response.end(JSON.stringify({ app: 'vindictus-dye-finder-web-preview', port })); return;
  }
  if (request.url === '/_preview/stop' && request.method === 'POST') {
    if (request.headers.authorization !== `Bearer ${token}`) { response.writeHead(403); response.end(); return; }
    response.end('Stopped'); server.close(); return;
  }
  if (!['GET', 'HEAD'].includes(request.method)) { response.writeHead(405); response.end(); return; }
  try {
    const path = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname);
    const file = resolve(root, `.${path === '/' ? '/index.html' : path}`);
    if (relative(root, file).startsWith('..') || !existsSync(file) || !statSync(file).isFile()) { response.writeHead(404); response.end('Not found'); return; }
    response.setHeader('Content-Type', mime[extname(file)] || 'text/plain; charset=utf-8');
    response.setHeader('Content-Security-Policy', "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; worker-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'none'");
    response.end(request.method === 'HEAD' ? undefined : readFileSync(file));
  } catch { response.writeHead(400); response.end('Invalid request'); }
});
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
server.on('close', () => { try { const saved = JSON.parse(readFileSync(resolve(state, `${port}.json`), 'utf8')); if (saved.token === token) unlinkSync(resolve(state, `${port}.json`)); } catch {} });
server.listen(port, '127.0.0.1', () => {
  mkdirSync(state, { recursive: true }); writeFileSync(resolve(state, `${port}.json`), JSON.stringify({ port, token }));
  console.log(`Dye Finder Web Preview: http://127.0.0.1:${port}/`);
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close());
