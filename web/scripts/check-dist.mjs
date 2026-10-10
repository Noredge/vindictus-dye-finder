import { readdirSync, readFileSync, statSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { createHash } from 'node:crypto';

const root = resolve('dist');
const inventory = [];
function visit(dir) {
  for (const name of readdirSync(dir)) {
    const file = resolve(dir, name);
    if (statSync(file).isDirectory()) { visit(file); continue; }
    const path = relative(root, file).replaceAll('\\', '/');
    if (!/^(index\.html|version\.json|demo\.png|icon\.png|LICENSE|ASSET-NOTICES\.md|THIRD-PARTY-NOTICES\.txt|assets\/[\w-]+\.(js|css))$/.test(path)) throw new Error(`Unexpected deployment file: ${path}`);
    const bytes = readFileSync(file);
    if (/\.(html|js|css|md|txt)$/.test(path) && /C:[\\/]+Users[\\/]+|private-data|WEB-MIGRATION-PLAN|sourceMappingURL/.test(bytes.toString('utf8'))) throw new Error(`Private path or internal artifact in ${path}`);
    inventory.push({ path, bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') });
  }
}
visit(root);
for (const expected of ['index.html', 'demo.png', 'icon.png', 'LICENSE', 'ASSET-NOTICES.md', 'THIRD-PARTY-NOTICES.txt']) if (!inventory.some(f => f.path === expected)) throw new Error(`Missing ${expected}`);
if (!inventory.some(f => /^assets\/analyzer-.*\.js$/.test(f.path))) throw new Error('Worker bundle missing.');
console.log(JSON.stringify({ totalBytes: inventory.reduce((n, f) => n + f.bytes, 0), files: inventory }, null, 2));
