// Medan jarak bertanda (SDF) jalan, air, dan pelataran pada kisi 0,25 unit, plus collider hasil gabungan sel.
// Titik kisi ke-(i, j) berada di origin + (i, j) * cell. Baris 0 = y terkecil.
import { resample, segDist, roundBoxSdf, polySdf, chaikin, unionRound } from './geom.mjs';
import { noise2 } from './rng.mjs';

export const CELL = 0.25;
const QUERY = 8;            // radius pencarian ruas (unit)
const BUCKET = 4;

export function makeGrid(extent) {
  const [x0, y0, x1, y1] = extent;
  const W = Math.round((x1 - x0) / CELL), H = Math.round((y1 - y0) / CELL);
  return { x0, y0, W, H, nx: W + 1, ny: H + 1, cell: CELL };
}

export function plazaSdf(plaza, p) {
  const [cx, cy] = plaza.center, [w, h] = plaza.size, r = plaza.radius ?? 2;
  const radii = plaza.radii ?? [r, r, r, r];
  return roundBoxSdf(p, [cx, cy], [w / 2, h / 2], radii);
}

// Aspal depot = parkiran (kotak) + jalan masuk (kapsul dari tepi parkiran ke garis tengah jalan).
// Jalan masuk bukan ruas graf, jadi tidak menambah jalan buntu; mulutnya dilebur ke aspal oleh fillet pelataran.
// Lantai terang (kanal plaza) hanya parkirannya: jalan masuk beraspal hitam seperti jalan, jadi tidak ada lantai
// terang yang keluar ke jalan.
export function depotSdf(depot, p) {
  let d = plazaSdf(depot.plaza, p);
  const dw = depot.driveway;
  if (dw) d = unionRound(d, segDist(p, dw.from, dw.to) - dw.width / 2, dw.fillet ?? 0.8);
  return d;
}

export function lakeShapes(layout) {
  return (layout.lakes ?? []).map((l, i) => ({ ...l, poly: chaikin(l.shore, l.smooth ?? 3, true),
    ...(l.beachVary ? { vary: noise2((layout.seed ?? 1) + 11 + i, l.beachVary[1] ?? 20, 2) } : {}) }));
}

// Pantai: pita pasir selebar `beach` di darat sepanjang tepi air. Medan air di pita itu dibuat datar di SAND
// (0 < SAND < 0,35 = pita pasir shader/pratinjau), jadi pasir lebar tampil di Unity tanpa kanal atau shader baru.
// `beachVary` [amp, skala]: lebar pasir berubah pelan (±amp bagian) mengikuti noise.
export const SAND = 0.175;
export function shoreSdf(l, p) {
  const d = polySdf(p, l.poly);
  if (!l.beach) return d;
  const b = l.beach * (l.vary ? 1 + l.beachVary[0] * (2 * l.vary(p[0], p[1]) - 1) : 1);
  return Math.min(d, Math.max(SAND, d - b + SAND));
}

// Pantai sampai jalan (permintaan pengguna 26 Sep): darat di antara air dan jalan pesisir diisi pasir penuh sampai
// tepi jalan, tanpa sisa rumput. Sel jadi pasir kalau garis lurus ke tepi air tidak melewati aspal dan ada aspal di
// sisi darat dalam jangkauan `beachToRoad` dari air. Di tempat jalan menjauh dari laut, pasir kembali selebar `beach`.
const ROAD_EDGE = 0.6;  // aspal + garis tepi jalan (lihat pita tepi di raster/shader)
function beachToRoad(layout, g, pave, water, sea) {
  const reach = Math.max(0, ...(layout.lakes ?? []).map(l => l.beachToRoad ?? 0));
  if (!reach) return;
  const at = (arr, x, y) => {
    const i = Math.round((x - g.x0) / CELL), j = Math.round((y - g.y0) / CELL);
    return i < 0 || j < 0 || i >= g.nx || j >= g.ny ? 8 : arr[j * g.nx + i];
  };
  const fill = [];
  for (let j = 1; j < g.ny - 1; j++) for (let i = 1; i < g.nx - 1; i++) {
    const idx = j * g.nx + i, d = sea[idx];
    if (d <= 0 || d >= reach || water[idx] <= SAND || pave[idx] < 0) continue;
    let gx = sea[idx + 1] - sea[idx - 1], gy = sea[idx + g.nx] - sea[idx - g.nx];
    const len = Math.hypot(gx, gy);
    if (len < 1e-6) continue;
    gx /= len; gy /= len;
    const x = g.x0 + i * CELL, y = g.y0 + j * CELL;
    let open = true;
    for (let s = 0.5; s < d && open; s += 0.5) if (at(pave, x - gx * s, y - gy * s) < ROAD_EDGE) open = false;
    if (!open) continue;
    let road = false;
    for (let s = 0.5; s <= reach - d && !road; s += 0.5) if (at(pave, x + gx * s, y + gy * s) < ROAD_EDGE) road = true;
    if (road) fill.push(idx);
  }
  for (const idx of fill) water[idx] = SAND;
}

// SDF jalan: tiap goresan = kapsul sepanjang polyline, digabung dengan fillet membulat berjari-jari `fillet`.
export function computeFields(layout, net, strokes) {
  const g = makeGrid(layout.extent);
  const hw = net.halfWidth, fillet = layout.fillet ?? 2.6;
  const clampFar = QUERY - hw;
  const segs = [];
  strokes.forEach((s, si) => {
    const pts = resample(s.poly, 0.5, s.closed);
    const n = s.closed ? pts.length : pts.length - 1;
    for (let i = 0; i < n; i++) segs.push([si, pts[i], pts[(i + 1) % pts.length]]);
  });
  const bw = Math.ceil((g.W * CELL) / BUCKET) + 1, bh = Math.ceil((g.H * CELL) / BUCKET) + 1;
  const buckets = Array.from({ length: bw * bh }, () => []);
  segs.forEach((sg, k) => {
    const [, a, b] = sg;
    const bx0 = Math.max(0, Math.floor((Math.min(a[0], b[0]) - g.x0) / BUCKET)), bx1 = Math.min(bw - 1, Math.floor((Math.max(a[0], b[0]) - g.x0) / BUCKET));
    const by0 = Math.max(0, Math.floor((Math.min(a[1], b[1]) - g.y0) / BUCKET)), by1 = Math.min(bh - 1, Math.floor((Math.max(a[1], b[1]) - g.y0) / BUCKET));
    for (let by = by0; by <= by1; by++) for (let bx = bx0; bx <= bx1; bx++) buckets[by * bw + bx].push(k);
  });
  const pave = new Float32Array(g.nx * g.ny);
  const plaza = new Float32Array(g.nx * g.ny);
  const water = new Float32Array(g.nx * g.ny);
  // Jarak mentah ke tepi air yang pasirnya boleh melebar sampai jalan (`beachToRoad`).
  const sea = new Float32Array(g.nx * g.ny).fill(1e6);
  const best = new Float32Array(strokes.length).fill(Infinity);
  const touched = [];
  const lakes = lakeShapes(layout);
  const lakeBoxes = lakes.map(l => {
    const xs = l.poly.map(p => p[0]), ys = l.poly.map(p => p[1]);
    const pad = 8 + (l.beach ?? 0);
    return [Math.min(...xs) - pad, Math.min(...ys) - pad, Math.max(...xs) + pad, Math.max(...ys) + pad];
  });
  const depotDef = layout.depot?.plaza ? layout.depot : null;
  const r = Math.ceil(QUERY / BUCKET);
  for (let j = 0; j < g.ny; j++) {
    const y = g.y0 + j * CELL;
    for (let i = 0; i < g.nx; i++) {
      const x = g.x0 + i * CELL, p = [x, y];
      const bx = Math.floor((x - g.x0) / BUCKET), by = Math.floor((y - g.y0) / BUCKET);
      for (let yy = Math.max(0, by - r); yy <= Math.min(bh - 1, by + r); yy++)
        for (let xx = Math.max(0, bx - r); xx <= Math.min(bw - 1, bx + r); xx++)
          for (const k of buckets[yy * bw + xx]) {
            const [si, a, b] = segs[k];
            const d = segDist(p, a, b);
            if (d < best[si]) { if (best[si] === Infinity) touched.push(si); best[si] = d; }
          }
      let acc = clampFar;
      for (const si of touched) { acc = unionRound(acc, Math.min(best[si] - hw, clampFar), fillet); best[si] = Infinity; }
      touched.length = 0;
      const idx = j * g.nx + i;
      let pz = 8;
      if (depotDef) { pz = Math.min(plazaSdf(depotDef.plaza, p), 8); acc = unionRound(acc, Math.min(depotSdf(depotDef, p), 8), layout.plazaFillet ?? 1.4); }
      pave[idx] = acc;
      plaza[idx] = pz;
      let w = 8;
      lakes.forEach((l, li) => {
        const bb = lakeBoxes[li];
        if (x >= bb[0] && x <= bb[2] && y >= bb[1] && y <= bb[3]) {
          w = Math.min(w, shoreSdf(l, p));
          if (l.beachToRoad) sea[idx] = Math.min(sea[idx], polySdf(p, l.poly));
        }
      });
      water[idx] = w;
    }
  }
  beachToRoad(layout, g, pave, water, sea);
  return { grid: g, pave, plaza, water, lakes };
}

// Nilai bilinear dari kisi.
export function sampler(grid, arr) {
  return p => {
    const fx = (p[0] - grid.x0) / grid.cell, fy = (p[1] - grid.y0) / grid.cell;
    if (fx < 0 || fy < 0 || fx > grid.W || fy > grid.H) return 8;
    const i = Math.min(grid.W - 1, Math.floor(fx)), j = Math.min(grid.H - 1, Math.floor(fy));
    const tx = fx - i, ty = fy - j, n = grid.nx;
    const a = arr[j * n + i], b = arr[j * n + i + 1], c = arr[(j + 1) * n + i], d = arr[(j + 1) * n + i + 1];
    return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
  };
}

// Collider: sel 0,25 tertutup kalau SDF di tengah sel (rata-rata 4 sudut) >= WALL_EDGE. Sisanya digabung jadi persegi.
// Permintaan pengguna 26 Sep: truk menabrak di pita abu pinggir jalan, bukan masih di aspal hitam. Dulu sel tertutup
// begitu satu sudut keluar aspal, jadi dinding mulai sampai 0,25 di DALAM aspal. Sekarang muka dinding berada di
// SDF 0,2 ± 0,125 (± 0,18 di diagonal): di atas pita krem (0-0,22 di TownGround.shader), paling jauh sedikit
// masuk pita abu (0,22-0,48), tidak pernah sampai rumput.
export const WALL_EDGE = 0.2;
// Sel yang salah satu sudutnya masih di aspal tidak pernah jadi dinding: di tikungan/fillet, rata-rata saja
// menyisakan sudut tangga dinding sampai 0,075 di dalam aspal.
export const wallCell = (a, b, c, d) => (a + b + c + d) * 0.25 >= WALL_EDGE && Math.min(a, b, c, d) >= 0;
// Peta sel tertutup. `solids` = persegi [xmin, ymin, xmax, ymax] yang selalu padat (gedung gudang): dinding tepat di
// muka bangunan, supaya moncong truk tidak masuk gambar gedung walau pita pinggir di depannya boleh dilindas.
export function wallGrid(fields, solids = []) {
  const { grid: g, pave } = fields;
  const blocked = new Uint8Array(g.W * g.H);
  for (let j = 0; j < g.H; j++) for (let i = 0; i < g.W; i++) {
    const a = pave[j * g.nx + i], b = pave[j * g.nx + i + 1], c = pave[(j + 1) * g.nx + i], d = pave[(j + 1) * g.nx + i + 1];
    const cx = g.x0 + (i + 0.5) * CELL, cy = g.y0 + (j + 0.5) * CELL;
    blocked[j * g.W + i] = wallCell(a, b, c, d) || solids.some(r => cx > r[0] && cx < r[2] && cy > r[1] && cy < r[3]) ? 1 : 0;
  }
  return blocked;
}

export function bakeColliders(fields, solids = []) {
  const g = fields.grid;
  const blocked = wallGrid(fields, solids);
  const rects = [];
  for (let y = 0; y < g.H; y++) for (let x = 0; x < g.W; x++) {
    if (!blocked[y * g.W + x]) continue;
    let rw = 1; while (x + rw < g.W && blocked[y * g.W + x + rw]) rw++;
    let rh = 1;
    grow: while (y + rh < g.H) {
      for (let dx = 0; dx < rw; dx++) if (!blocked[(y + rh) * g.W + x + dx]) break grow;
      rh++;
    }
    for (let dy = 0; dy < rh; dy++) for (let dx = 0; dx < rw; dx++) blocked[(y + dy) * g.W + x + dx] = 0;
    rects.push([+(g.x0 + x * CELL).toFixed(2), +(g.y0 + y * CELL).toFixed(2), rw * CELL, rh * CELL]);
  }
  return rects;
}

// pavement-sdf.bytes: Int16 LE, nilai = jarak * 256.
export function encodeSdf(arr) {
  const buf = Buffer.alloc(arr.length * 2);
  for (let i = 0; i < arr.length; i++) buf.writeInt16LE(Math.max(-32767, Math.min(32767, Math.round(arr[i] * 256))), i * 2);
  return buf;
}

// ground-field.bytes: RGBA8. R = indeks kawasan, G = SDF air, B = SDF pelataran (128 + jarak*16), A = nada rumput.
export const quant = d => Math.max(0, Math.min(255, Math.round(128 + d * 16)));
export function encodeGround(fields, district, tone) {
  const n = fields.pave.length, buf = Buffer.alloc(n * 4);
  for (let k = 0; k < n; k++) {
    buf[k * 4] = district[k]; buf[k * 4 + 1] = quant(fields.water[k]); buf[k * 4 + 2] = quant(fields.plaza[k]); buf[k * 4 + 3] = tone[k];
  }
  return buf;
}
