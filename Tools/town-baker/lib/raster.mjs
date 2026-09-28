// Gambar cek cepat (dev) dan minimap dari data bake. Warna tanah sama dengan pratinjau HTML dan shader Unity.
import { sampler, CELL } from './field.mjs';
import { PROP_KINDS, HOUSE } from './place.mjs';

const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
export const GROUND = {
  grassA: hex('#78A355'), grassB: hex('#8BB464'), water: hex('#4F9BA6'), waterEdge: hex('#74B3B4'), sand: hex('#C9C18F'),
  asphalt: hex('#2B2E2C'), paving: hex('#D9DBD2'), seam: hex('#C3C5BB'), walk: hex('#EFE9D6'), curb: hex('#9A9B92'), dash: hex('#A8A998'),
};
export const mix = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
export const sstep = (e0, e1, x) => { const t = Math.max(0, Math.min(1, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t); };

// Warna tanah di satu titik. aa = setengah lebar piksel dalam unit (anti-alias).
export function groundColor(pave, water, plaza, tone, p, aa) {
  let c = mix(GROUND.grassA, GROUND.grassB, tone);
  if (water < 0.6) c = mix(c, GROUND.sand, 1 - sstep(0.35 - aa, 0.35 + aa, water) * 0.999);
  if (water < 0) c = mix(c, water > -0.7 ? GROUND.waterEdge : GROUND.water, 1 - sstep(-aa, aa, water));
  // Tepi jalan: pinggir abu (0,22..0,48), garis krem (0..0,22), lalu aspal/paving.
  if (pave < 0.48 + aa) c = mix(c, GROUND.curb, 1 - sstep(0.48 - aa, 0.48 + aa, pave));
  if (pave < 0.22 + aa) c = mix(c, GROUND.walk, 1 - sstep(0.22 - aa, 0.22 + aa, pave));
  if (pave < aa) {
    let top = GROUND.asphalt;
    if (plaza < 0.5) {
      const fx = ((p[0] % 1.333) + 1.333) % 1.333, fy = ((p[1] % 1.333) + 1.333) % 1.333;
      const seam = Math.min(fx, 1.333 - fx, fy, 1.333 - fy) < 0.035 ? GROUND.seam : GROUND.paving;
      top = mix(seam, GROUND.asphalt, sstep(-aa, aa, plaza));
    }
    c = mix(c, top, 1 - sstep(-aa, aa, pave));
  }
  return c;
}

function canvas(r, scale) {
  const [x0, y0, x1, y1] = r.layout.extent;
  const w = Math.round((x1 - x0) * scale), h = Math.round((y1 - y0) * scale);
  const data = new Uint8Array(w * h * 4);
  const toPx = p => [(p[0] - x0) * scale, (y1 - p[1]) * scale];
  const set = (px, py, c, a = 1) => {
    px = Math.round(px); py = Math.round(py);
    if (px < 0 || py < 0 || px >= w || py >= h) return;
    const k = (py * w + px) * 4;
    data[k] = data[k] * (1 - a) + c[0] * a; data[k + 1] = data[k + 1] * (1 - a) + c[1] * a; data[k + 2] = data[k + 2] * (1 - a) + c[2] * a; data[k + 3] = 255;
  };
  return { w, h, data, toPx, set, x0, y1 };
}

function paintGround(r, cv, scale) {
  const g = r.fields.grid;
  const pave = sampler(g, r.fields.pave), water = sampler(g, r.fields.water), plaza = sampler(g, r.fields.plaza);
  const toneArr = r.toneArr;
  const tone = p => { const i = Math.round((p[0] - g.x0) / CELL), j = Math.round((p[1] - g.y0) / CELL); return (toneArr[Math.max(0, Math.min(g.ny - 1, j)) * g.nx + Math.max(0, Math.min(g.nx - 1, i))] ?? 128) / 255; };
  const aa = 0.5 / scale;
  for (let py = 0; py < cv.h; py++) for (let px = 0; px < cv.w; px++) {
    const p = [cv.x0 + (px + 0.5) / scale, cv.y1 - (py + 0.5) / scale];
    const c = groundColor(pave(p), water(p), plaza(p), tone(p), p, aa);
    const k = (py * cv.w + px) * 4;
    cv.data[k] = c[0]; cv.data[k + 1] = c[1]; cv.data[k + 2] = c[2]; cv.data[k + 3] = 255;
  }
}

const ROOF = { bata: hex('#C4553A'), laut: hex('#2E86C9'), daun: hex('#4F9A45'), senja: hex('#8D5BB5') };
const PROP_COLOR = { pohon: hex('#2F6B35'), pohon2: hex('#3E7E3A'), semak: hex('#5FA14E'), batu: hex('#A7A89E'), bedeng: hex('#E27A93'),
  lampu: hex('#F4C63F'), kotakSurat: hex('#C24A33'), bangku: hex('#9A6A45'), pagar: hex('#E6DCC0'), dermaga: hex('#9A6A45'),
  lapak: hex('#E8483B'), taman: hex('#F59A2C'), papanSekolah: hex('#1DB4E8'), airMancur: hex('#569AA2') };

function disc(cv, c, r, color, a = 1, scale) {
  const [cx, cy] = cv.toPx(c), rp = r * scale;
  for (let y = -rp; y <= rp; y++) for (let x = -rp; x <= rp; x++) if (x * x + y * y <= rp * rp) cv.set(cx + x, cy + y, color, a);
}
function box(cv, c, angleDeg, hw, hh, color, a, scale) {
  const ang = angleDeg * Math.PI / 180, ca = Math.cos(ang), sa = Math.sin(ang);
  const R = Math.hypot(hw, hh);
  const [cx, cy] = cv.toPx(c);
  for (let y = -R * scale; y <= R * scale; y++) for (let x = -R * scale; x <= R * scale; x++) {
    const wx = x / scale, wy = -y / scale;
    const u = wx * ca + wy * sa, v = -wx * sa + wy * ca;
    if (Math.abs(u) <= hw && Math.abs(v) <= hh) cv.set(cx + x, cy + y, color, a);
  }
}
function line(cv, a, b, color, scale, width = 1) {
  const n = Math.ceil(Math.hypot(b[0] - a[0], b[1] - a[1]) * scale * 2) + 1;
  for (let i = 0; i <= n; i++) {
    const p = [a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n];
    const [x, y] = cv.toPx(p);
    for (let dy = -width + 1; dy < width; dy++) for (let dx = -width + 1; dx < width; dx++) cv.set(x + dx, y + dy, color);
  }
}

export function renderDebug(r, scale = 3) {
  const cv = canvas(r, scale);
  paintGround(r, cv, scale);
  for (const [x0, y0, x1, y1] of r.baked.markings) line(cv, [x0, y0], [x1, y1], GROUND.dash, scale);
  const d = r.layout.depot;
  if (d) {
    box(cv, d.building.center, 0, d.building.size[0] / 2, d.building.size[1] / 2, hex('#B85A3A'), 1, scale);
    box(cv, d.finish.center, d.finish.angle ?? 0, d.finish.size[0] / 2, d.finish.size[1] / 2, hex('#1DB4E8'), 0.35, scale);
    disc(cv, d.pickup, 1.6, hex('#F09A2C'), 0.7, scale);
  }
  for (const h of r.baked.houses) {
    box(cv, h.center, h.angle, HOUSE.halfW, HOUSE.halfD, hex('#9CC27A'), 0.8, scale);
    box(cv, h.center, h.angle, 2.1, 2.1, h.tutorial ? hex('#1DB4E8') : ROOF[h.colorway] ?? ROOF.bata, 1, scale);
    disc(cv, h.door, 0.5, hex('#FFFFFF'), 1, scale);
    disc(cv, h.stop, 0.6, hex('#1DB4E8'), 0.8, scale);
  }
  for (const p of r.baked.props) disc(cv, p.pos, Math.max(0.35, PROP_KINDS[p.kind].r * 0.8), PROP_COLOR[p.kind], 1, scale);
  for (const n of r.baked.nodes) disc(cv, n.pos, n.junction ? 1 : 0.6, hex('#FF2D55'), 0.9, scale);
  return { width: cv.w, height: cv.h, data: cv.data };
}

// Bentuk halus (anti-alias) untuk minimap detail: persegi bersudut bulat yang diputar, dan lingkaran.
function softBox(cv, c, angleDeg, hw, hh, radius, color, a, scale) {
  const ang = angleDeg * Math.PI / 180, ca = Math.cos(ang), sa = Math.sin(ang);
  const R = Math.hypot(hw, hh) + 1 / scale;
  const [cx, cy] = cv.toPx(c);
  for (let y = Math.floor(-R * scale); y <= R * scale; y++) for (let x = Math.floor(-R * scale); x <= R * scale; x++) {
    const px = Math.floor(cx) + x, py = Math.floor(cy) + y;
    const wx = (px + 0.5 - cx) / scale, wy = -(py + 0.5 - cy) / scale;
    const u = Math.abs(wx * ca + wy * sa) - hw + radius, v = Math.abs(-wx * sa + wy * ca) - hh + radius;
    const d = Math.min(Math.max(u, v), 0) + Math.hypot(Math.max(u, 0), Math.max(v, 0)) - radius;
    const cover = Math.max(0, Math.min(1, 0.5 - d * scale));
    if (cover > 0) cv.set(px, py, color, a * cover);
  }
}
const softDisc = (cv, c, r, color, a, scale) => softBox(cv, c, 0, r, r, r, color, a, scale);

// Detail minimap UI (permintaan pengguna 26 Sep): pohon, rumah (warna atap), dan gedung gudang, diredam ke arah
// rumput supaya penanda tujuan, rute, dan truk yang digambar saat runtime tetap paling menonjol.
function paintMinimapDetail(r, cv, scale, grass) {
  const tree = mix(grass, hex('#2F6B35'), 0.45);
  for (const p of r.baked.props) if (PROP_KINDS[p.kind]?.tree) softDisc(cv, p.pos, PROP_KINDS[p.kind].r * 0.75, tree, 0.9, scale);
  const edge = hex('#4E6B3E');
  for (const h of r.baked.houses) {
    const roof = mix(h.tutorial ? hex('#1DB4E8') : ROOF[h.colorway] ?? ROOF.bata, hex('#FFFFFF'), 0.12);
    softBox(cv, h.center, h.angle, 2.35, 2.35, 0.7, edge, 0.55, scale);
    softBox(cv, h.center, h.angle, 2, 2, 0.5, roof, 1, scale);
  }
  const d = r.layout.depot;
  if (d?.building) softBox(cv, d.building.center, 0, d.building.size[0] / 2, d.building.size[1] / 2, 0.8, hex('#C4553A'), 1, scale);
}

// Minimap: jalan terang di atas rumput, air, dan pelataran. Penanda digambar saat runtime.
// detail = rumah, pohon, dan gedung gudang ikut digambar (minimap UI di HUD).
export function renderMinimap(r, scale = 2, { detail = false } = {}) {
  const cv = canvas(r, scale);
  const g = r.fields.grid;
  const pave = sampler(g, r.fields.pave), water = sampler(g, r.fields.water), plaza = sampler(g, r.fields.plaza);
  const aa = 0.5 / scale;
  // Pasir pantai hanya di peta yang punya pantai; pita tipis tepi danau biasa tidak diwarnai.
  const beach = r.fields.lakes.some(l => l.beach);
  const cGrass = hex('#8DB86A'), cRoad = hex('#FBF8EE'), cEdge = hex('#5E7F4A'), cWater = hex('#6CB4C6'), cPlaza = hex('#F2C58A'), cSand = hex('#E3D7A4');
  for (let py = 0; py < cv.h; py++) for (let px = 0; px < cv.w; px++) {
    const p = [cv.x0 + (px + 0.5) / scale, cv.y1 - (py + 0.5) / scale];
    let c = cGrass;
    const w = water(p), pv = pave(p), pz = plaza(p);
    if (beach && w < 0.35 + aa) c = mix(c, cSand, 1 - sstep(0.35 - aa, 0.35 + aa, w));
    c = mix(c, cWater, 1 - sstep(-aa, aa, w));
    c = mix(c, cEdge, 1 - sstep(0.6 - aa, 0.6 + aa, pv));
    c = mix(c, pz < 0 ? cPlaza : cRoad, 1 - sstep(-0.2 - aa, -0.2 + aa, pv));
    const k = (py * cv.w + px) * 4;
    cv.data[k] = c[0]; cv.data[k + 1] = c[1]; cv.data[k + 2] = c[2]; cv.data[k + 3] = 255;
  }
  if (detail) paintMinimapDetail(r, cv, scale, cGrass);
  return { width: cv.w, height: cv.h, data: cv.data };
}
