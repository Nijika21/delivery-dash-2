// Membuat varian preview skin dari PNG lewat filter SVG (resvg).
// Pemakaian:
//   node Tools/art/skin-preview.mjs <masuk.png> <keluar.png> locked      → abu-abu terang untuk skin terkunci
//   node Tools/art/skin-preview.mjs <masuk.png> <keluar.png> hue:<derajat> → putar warna (hanya untuk mock desain)
// Di Unity, TruckSkinBaker v2 membake varian "locked" dengan rumus yang sama.
import { readFileSync, writeFileSync } from 'node:fs';
import { Resvg } from '@resvg/resvg-js';

const [input, output, mode = 'locked'] = process.argv.slice(2);
if (!input || !output) { console.error('pakai: skin-preview.mjs <masuk.png> <keluar.png> [locked|hue:<deg>]'); process.exit(2); }

const png = readFileSync(input);
const w = png.readUInt32BE(16), h = png.readUInt32BE(20);
const href = 'data:image/png;base64,' + png.toString('base64');

// locked: luminans lalu diangkat ke abu-abu terang (y = 0.45·L + 0.42), alfa tetap.
const filter = mode === 'locked'
  ? `<feColorMatrix type="saturate" values="0"/>
     <feComponentTransfer>
       <feFuncR type="linear" slope="0.45" intercept="0.42"/>
       <feFuncG type="linear" slope="0.45" intercept="0.44"/>
       <feFuncB type="linear" slope="0.45" intercept="0.46"/>
     </feComponentTransfer>`
  : `<feColorMatrix type="hueRotate" values="${Number(mode.split(':')[1] || 0)}"/>`;

const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">
  <defs><filter id="f" x="0" y="0" width="1" height="1" color-interpolation-filters="sRGB">${filter}</filter></defs>
  <image href="${href}" width="${w}" height="${h}" filter="url(#f)"/>
</svg>`;
writeFileSync(output, new Resvg(svg).render().asPng());
console.log(`${output} (${w}x${h}, ${mode})`);
