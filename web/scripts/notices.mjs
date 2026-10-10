import { readFileSync, writeFileSync } from 'node:fs';

const packages = ['react', 'react-dom', 'scheduler', 'fflate'];
const text = packages.map(name => {
  const info = JSON.parse(readFileSync(`node_modules/${name}/package.json`, 'utf8'));
  return `${name} ${info.version}\n${readFileSync(`node_modules/${name}/LICENSE`, 'utf8')}\n`;
}).join('\n--------------------\n\n');
writeFileSync('public/THIRD-PARTY-NOTICES.txt', text);
const app = JSON.parse(readFileSync('package.json', 'utf8'));
writeFileSync('public/version.json', JSON.stringify({ version: app.version }));
