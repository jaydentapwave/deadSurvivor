// Render existing preparation SVGs and record exact output dependencies. No Unity edits.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require(require.resolve('sharp', { paths: [process.env.DS_NODE_MODULES || '/Users/hyeongwoo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules'] }));
const root = path.resolve(__dirname, '../../../..');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
(async () => {
  const files = {};
  for (const kind of ['structure','collision','tests','changes']) {
    const svg = path.join(__dirname, `map-meadow-007-r001-${kind}.svg`);
    const png = svg.replace(/\.svg$/, '.png');
    await sharp(svg).png().toFile(png);
    files[path.relative(root, svg)] = hash(svg);
    files[path.relative(root, png)] = hash(png);
  }
  fs.writeFileSync(path.join(__dirname, 'render-manifest.json'), JSON.stringify({
    scope: 'preparation SVG/PNG integrity only',
    layout_sha256: hash(path.join(root,'docs/design/map-meadow-007-r001-layout.json')),
    files
  },null,2)+'\n');
})().catch(error => { console.error(error); process.exitCode = 1; });
