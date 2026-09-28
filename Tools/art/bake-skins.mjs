// Mock desain TruckSkinBaker v2: mewarnai ulang bodi cyan car.png dan menempel motif.
// Pemakaian: node Tools/art/bake-skins.mjs <folder-keluar> [lebar-preview]
// Menulis <id>.png (ukuran preview) dan <id>-kunci.png (abu-abu terang untuk kartu garasi terkunci).
// Unity (I2) memakai rumus yang sama: bobot cyan dari hue/saturasi, nada tepi→isi dari kanal G, motif dipotong ke bak.
import { readPng, encodePng, rgbToHsv } from './png.mjs';
import { Resvg } from '@resvg/resvg-js';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SKINS, INNER } from '../../ArtSource/skins/skins.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [outDir, previewWidth = '162'] = process.argv.slice(2);
if (!outDir) { console.error('pakai: bake-skins.mjs <folder-keluar> [lebar-preview]'); process.exit(2); }
mkdirSync(outDir, { recursive: true });

const base = readPng(join(ROOT, 'Assets/env1/car.png'));
const { width: W, height: H } = base;
const EDGE_G = 179, FILL_G = 203; // nada tepi (0,179,230) dan isi (37,203,243) bodi asli

const hex = c => [1, 3, 5].map(i => parseInt(c.slice(i, i + 2), 16));
const clamp01 = v => Math.max(0, Math.min(1, v));
const smooth = (e0, e1, x) => { const t = clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); };

function insideRoundRect(x, y, { x: rx, y: ry, w, h, r }) {
  if (x < rx || y < ry || x > rx + w || y > ry + h) return false;
  const cx = Math.min(Math.max(x, rx + r), rx + w - r), cy = Math.min(Math.max(y, ry + r), ry + h - r);
  return (x - cx) ** 2 + (y - cy) ** 2 <= r * r;
}

// Bobot "bagian bodi" per piksel, dihitung sekali.
const weight = new Float32Array(W * H), tone = new Float32Array(W * H);
for (let i = 0; i < W * H; i++) {
  const d = base.data, j = i * 4;
  if (!d[j + 3]) continue;
  const [h, s, v] = rgbToHsv(d[j], d[j + 1], d[j + 2]);
  // Kaca depan juga kebiruan tapi gelap; syarat terang menjaganya tetap asli.
  weight[i] = h > 0.47 && h < 0.61 ? smooth(0.2, 0.45, s) * smooth(0.55, 0.75, v) : 0;
  tone[i] = clamp01((d[j + 1] - EDGE_G) / (FILL_G - EDGE_G));
}

function renderMotif(svgBody) {
  const { x, y, w, h, r } = INNER;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
    <defs><clipPath id="bak"><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}"/></clipPath></defs>
    <g clip-path="url(#bak)">${svgBody}</g></svg>`;
  const img = new Resvg(svg).render();
  return { data: img.pixels, width: img.width, height: img.height };
}

export function bakeSkin(skin) {
  const out = new Uint8ClampedArray(base.data);
  if (!skin.fill) return { width: W, height: H, data: out };
  const fill = hex(skin.fill), edge = hex(skin.edge);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = y * W + x, w = weight[i];
    if (!w) continue;
    const t = skin.hideStripes && insideRoundRect(x, y, { ...INNER, r: INNER.r + 4 }) ? 1 : tone[i];
    for (let c = 0; c < 3; c++) {
      const target = edge[c] + (fill[c] - edge[c]) * t;
      out[i * 4 + c] = out[i * 4 + c] + (target - out[i * 4 + c]) * w;
    }
  }
  if (skin.motif) {
    const m = renderMotif(skin.motif()).data; // piksel resvg = RGBA premultiplied
    for (let i = 0; i < W * H; i++) {
      const a = (m[i * 4 + 3] / 255) * weight[i];
      if (!a) continue;
      const pa = m[i * 4 + 3] / 255;
      for (let c = 0; c < 3; c++) {
        const src = pa ? m[i * 4 + c] / pa : 0; // buang premultiply
        out[i * 4 + c] = out[i * 4 + c] + (src - out[i * 4 + c]) * a;
      }
    }
  }
  return { width: W, height: H, data: out };
}

// Rumus preview terkunci, sama dengan skin-preview.mjs (saturate 0 lalu diangkat ke abu-abu terang).
export function lockedOf(img) {
  const out = new Uint8ClampedArray(img.data);
  for (let i = 0; i < out.length; i += 4) {
    const L = (0.2126 * out[i] + 0.7152 * out[i + 1] + 0.0722 * out[i + 2]) / 255;
    out[i] = (0.45 * L + 0.42) * 255; out[i + 1] = (0.45 * L + 0.44) * 255; out[i + 2] = (0.45 * L + 0.46) * 255;
  }
  return { ...img, data: out };
}

function downscale(img, width) {
  // Lewat resvg supaya hasilnya halus, tanpa dependensi tambahan.
  const height = Math.round(img.height * width / img.width);
  const href = 'data:image/png;base64,' + encodePng(img).toString('base64');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><image href="${href}" width="${width}" height="${height}"/></svg>`;
  return new Resvg(svg).render().asPng();
}

const wPrev = Number(previewWidth);
for (const skin of SKINS) {
  const img = bakeSkin(skin);
  writeFileSync(join(outDir, `${skin.id}.png`), downscale(img, wPrev));
  if (skin.price > 0 || skin.unlock) writeFileSync(join(outDir, `${skin.id}-kunci.png`), downscale(lockedOf(img), wPrev));
}
console.log(`skin: ${SKINS.length} → ${outDir}`);
