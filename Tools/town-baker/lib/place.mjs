// Penempatan rumah, properti, marka, dan anchor depot. Semua berbenih dan diperiksa aturannya.
import { add, sub, mul, dot, dist, norm, perp, resample, pointAt, curvatureProfile, pointInPoly } from './geom.mjs';
import { rng, noise2 } from './rng.mjs';
import { sampler, depotSdf } from './field.mjs';
import { nodeEnds } from './graph.mjs';

export const PROP_KINDS = {
  pohon: { r: 1.5, h: 3.1, tree: true },
  pohon2: { r: 1.4, h: 3.0, tree: true },
  semak: { r: 0.8, h: 1.2 },
  batu: { r: 0.6, h: 0.9 },
  bedeng: { r: 0.9, h: 1.0 },
  lampu: { r: 0.4, h: 2.4 },
  kotakSurat: { r: 0.35, h: 0.8 },
  bangku: { r: 0.8, h: 1.0 },
  pagar: { r: 1.0, h: 0.9 },
  dermaga: { r: 2.0, h: 1.4 },
  lapak: { r: 1.6, h: 2.4 },
  taman: { r: 2.4, h: 3.2 },
  papanSekolah: { r: 1.0, h: 2.0 },
  airMancur: { r: 8.6, h: 0.4, flat: true, size: 18 },  // datar di tanah, tampak atas
};
export const HOUSE = { setback: 7.6, halfW: 3.1, halfD: 3.4, height: 4.4, doorOffset: 1.9, stopOffset: 1.5 };
export const SCREENS = { landscape: [23.1, 13], portrait: [12.4, 22] };

const deg = rad => rad * 180 / Math.PI;

// Indeks titik jalan (tiap 1 unit) untuk mencari jalan terdekat dengan cepat.
function roadIndex(net) {
  const pts = [];
  net.edges.forEach((e, ei) => {
    const res = resample(e.poly, 1);
    const prof = curvatureProfile(res, 3);
    res.forEach((p, k) => {
      const q = res[Math.min(res.length - 1, k + 1)], o = res[Math.max(0, k - 1)];
      pts.push({ p, ei, s: k * e.length / (res.length - 1), dir: norm(sub(q, o)), radius: prof[k] });
    });
  });
  const B = 6, map = new Map();
  const keyOf = (x, y) => `${Math.floor(x / B)},${Math.floor(y / B)}`;
  for (const q of pts) { const k = keyOf(q.p[0], q.p[1]); if (!map.has(k)) map.set(k, []); map.get(k).push(q); }
  return (p, reach = 18) => {
    let best = null, bd = Infinity;
    const r = Math.ceil(reach / B), bx = Math.floor(p[0] / B), by = Math.floor(p[1] / B);
    for (let y = by - r; y <= by + r; y++) for (let x = bx - r; x <= bx + r; x++) {
      for (const q of map.get(`${x},${y}`) ?? []) { const d = dist(p, q.p); if (d < bd) { bd = d; best = q; } }
    }
    return best ? { ...best, d: bd } : null;
  };
}

function inside(extent, p, margin) {
  return p[0] > extent[0] + margin && p[0] < extent[2] - margin && p[1] > extent[1] + margin && p[1] < extent[3] - margin;
}

// Jarak ke kotak berputar (sumbu t sepanjang jalan, n ke arah rumah).
function boxDist(p, c, t, n, hw, hd) {
  const q = sub(p, c), u = Math.abs(dot(q, t)) - hw, v = Math.abs(dot(q, n)) - hd;
  return Math.min(Math.max(u, v), 0) + Math.hypot(Math.max(u, 0), Math.max(v, 0));
}

// Zona: { circle: [x, y, r] }, { rect: [x0, y0, x1, y1] }, atau { poly: [[x, y], ...] } (poly tanpa pad).
function zoneTest(zones) {
  return (p, pad = 0) => zones.some(z => z.rect
    ? p[0] > z.rect[0] - pad && p[0] < z.rect[2] + pad && p[1] > z.rect[1] - pad && p[1] < z.rect[3] + pad
    : z.poly ? pointInPoly(p, z.poly) : dist(p, z.circle) < z.circle[2] + pad);
}

function reservedTest(layout) {
  const zones = [...(layout.reserved ?? [])];
  const b = layout.depot?.building;
  if (b) zones.push({ rect: [b.center[0] - b.size[0] / 2, b.center[1] - b.size[1] / 2, b.center[0] + b.size[0] / 2, b.center[1] + b.size[1] / 2] });
  return zoneTest(zones);
}

export function districtLocator(layout) {
  const ds = layout.districts;
  return p => {
    for (let i = 0; i < ds.length; i++) {
      const c = ds[i].core;
      if (!c) continue;
      if (c.ring) {
        const r = layout.rings.find(x => x.id === c.ring), s = c.scale ?? 1.2;
        if (Math.abs((p[0] - r.center[0]) / (r.a * s)) ** r.n + Math.abs((p[1] - r.center[1]) / (r.b * s)) ** r.n < 1) return i;
      }
      if (c.rect && p[0] > c.rect[0] && p[0] < c.rect[2] && p[1] > c.rect[1] && p[1] < c.rect[3]) return i;
    }
    let best = 0, bd = Infinity;
    ds.forEach((d, i) => { if (d.core) return; const dd = dist(p, d.center); if (dd < bd) { bd = dd; best = i; } });
    return best;
  };
}

export function placeTown(layout, net, fields, junctions) {
  const R = rng(layout.seed ?? 1);
  const pave = sampler(fields.grid, fields.pave), water = sampler(fields.grid, fields.water);
  // Jarak ke lahan depot (parkiran + jalan masuk). Kanal plaza hanya parkiran, jadi jalan masuk dihitung langsung.
  const plaza = layout.depot?.plaza ? p => depotSdf(layout.depot, p) : sampler(fields.grid, fields.plaza);
  const nearestRoad = roadIndex(net);
  const reserved = reservedTest(layout);
  // Zona tanpa rumah (mis. sekeliling danau selain sisi kiri): properti tetap boleh, hanya kavling rumah yang dilarang.
  const noHouses = zoneTest(layout.noHouses ?? []);
  const districtOf = districtLocator(layout);
  const hw = net.halfWidth;
  const jPts = junctions.map(j => j.pos);
  const nearJunction = (p, r) => jPts.some(q => dist(p, q) < r);
  const checks = [];
  const check = (id, label, pass, value, limit) => checks.push({ id, label, pass, value, limit });
  const ext = layout.extent;

  // ---------- rumah ----------
  const cands = [];
  net.edges.forEach((e, ei) => {
    if (e.noHouses) return;
    for (let s = 6; s <= e.length - 6; s += 1.25) for (const side of [1, -1]) {
      const { p, dir } = pointAt(e.poly, s);
      const n = mul(perp(dir), side);
      const center = add(p, mul(n, HOUSE.setback));
      const stop = add(p, mul(n, HOUSE.stopOffset));
      if (nearJunction(stop, 10) || pave(stop) > -1.2 || plaza(stop) < 2) continue;
      if (!inside(ext, center, (layout.treeBand ?? 9) + 3.6) || noHouses(center)) continue;
      let ok = true;
      for (let a = -1; a <= 1 && ok; a += 0.5) for (let b = -1; b <= 1 && ok; b += 0.5) {
        const q = add(center, add(mul(dir, a * HOUSE.halfW), mul(n, b * HOUSE.halfD)));
        if (pave(q) < 0.7 || water(q) < 1.2 || plaza(q) < 1.2 || reserved(q, 0.5)) ok = false;
      }
      if (!ok) continue;
      const own = nearestRoad(center);
      if (own && own.ei !== ei && own.d < HOUSE.setback - 0.4) continue;
      cands.push({ ei, s, side, center, dir, n, stop, district: districtOf(center) });
    }
  });
  R.shuffle(cands);
  const houses = [];
  // Jarak antarrumah per kawasan (`spacing`): kawasan tengah bisa dibuat lebih lega daripada kampung.
  const gapOf = di => layout.districts[di].spacing ?? layout.houseSpacing ?? 7.6;
  const conflicts = c => houses.some(h => dist(h.center, c.center) < Math.max(gapOf(h.district), gapOf(c.district)) || dist(h.stop, c.stop) < 7);
  layout.districts.forEach((d, di) => {
    const pool = cands.filter(c => c.district === di);
    for (let q = 0; q < (d.houses ?? 0); q++) {
      let best = null, bestScore = -1;
      for (const c of pool) {
        if (c.dead) continue;
        if (conflicts(c)) { c.dead = true; continue; }
        let score = 80;
        for (const h of houses) score = Math.min(score, dist(h.center, c.center));
        score += R() * 2;
        if (score > bestScore) { bestScore = score; best = c; }
      }
      if (!best) break;
      best.dead = true;
      houses.push(best);
    }
  });
  const types = layout.houseTypes ?? { pelana: 1 };
  const colorways = layout.colorways ?? ['bata', 'laut', 'daun', 'senja'];
  houses.forEach(h => {
    const d = layout.districts[h.district];
    for (let tries = 0; tries < 12; tries++) {
      h.type = R.weighted(d.types ?? types);
      h.colorway = R.pick(colorways);
      const twin = houses.find(o => o !== h && o.type === h.type && o.colorway === h.colorway && dist(o.center, h.center) < 16);
      if (!twin) break;
    }
  });
  const pickup = layout.depot?.pickup;
  if (pickup) {
    const core = houses.filter(h => layout.districts[h.district].core).sort((a, b) => dist(a.center, pickup) - dist(b.center, pickup));
    if (core[0]) { core[0].type = 'tutorial'; core[0].colorway = 'laut'; core[0].tutorial = true; }
  }
  // Nama alamat: nama jalan + nomor urut sepanjang jalan.
  const perRoad = {};
  [...houses].sort((a, b) => a.ei - b.ei || a.s - b.s).forEach(h => {
    const name = net.edges[h.ei].name;
    perRoad[name] = (perRoad[name] ?? 0) + 1;
    h.name = `${name} ${perRoad[name]}`;
  });
  const range = layout.houseRange ?? [64, 80];
  check('rumah-total', 'Jumlah rumah', houses.length >= range[0] && houses.length <= range[1], houses.length, `${range[0]}–${range[1]}`);
  const short = layout.districts.map((d, di) => [d.name, houses.filter(h => h.district === di).length, d.houses ?? 0]).filter(([, got, want]) => got < want);
  check('rumah-kawasan', 'Kuota rumah tiap kawasan terpenuhi', short.length === 0, short.length ? short.map(([n, g, w]) => `${n} ${g}/${w}`).join(', ') : 'semua', 'semua');

  const lotDist = p => { let m = Infinity; for (const h of houses) m = Math.min(m, boxDist(p, h.center, h.dir, h.n, HOUSE.halfW, HOUSE.halfD)); return m; };
  const doorPath = p => houses.some(h => { const a = add(h.center, mul(h.n, -HOUSE.doorOffset)); return segDistSimple(p, a, h.stop) < 1.4; });

  // ---------- properti ----------
  const props = [];
  const B = 4, pmap = new Map();
  const pkey = (x, y) => `${Math.floor(x / B)},${Math.floor(y / B)}`;
  // Properti besar (air mancur) melebihi jangkauan ember, jadi disimpan terpisah dan selalu ikut dicek.
  const big = [];
  const addProp = pr => { props.push(pr); if (PROP_KINDS[pr.kind].r > 3) { big.push(pr); return; } const k = pkey(pr.pos[0], pr.pos[1]); if (!pmap.has(k)) pmap.set(k, []); pmap.get(k).push(pr); };
  const crowded = (p, r, gap) => {
    const bx = Math.floor(p[0] / B), by = Math.floor(p[1] / B);
    for (let y = by - 2; y <= by + 2; y++) for (let x = bx - 2; x <= bx + 2; x++)
      for (const o of pmap.get(`${x},${y}`) ?? []) if (dist(o.pos, p) < r + PROP_KINDS[o.kind].r + gap) return true;
    return big.some(o => dist(o.pos, p) < r + PROP_KINDS[o.kind].r + gap);
  };
  const valid = (kind, p, { paveMin, waterMin, gap = 0.6, lot = true, fixed = false } = {}) => {
    const k = PROP_KINDS[kind];
    if (!inside(ext, p, 1)) return false;
    if (k.r > 3) {
      // SDF aspal/air dijepit di 5 unit, jadi properti besar dicek di sepanjang tepinya.
      for (let a = 0; a < 24; a++) {
        const q = add(p, [Math.cos(a * Math.PI / 12) * k.r, Math.sin(a * Math.PI / 12) * k.r]);
        if (pave(q) < 0.6 || water(q) < 0.4) return false;
      }
    } else {
      if (pave(p) < (paveMin ?? (k.tree ? 2.2 : k.r + 0.6))) return false;
      if (water(p) < (waterMin ?? k.r + 0.4)) return false;
    }
    if (plaza(p) < k.r + 0.6) return false;
    if (!fixed && reserved(p, k.r)) return false;  // zona cadangan memang disiapkan untuk fitur tetap
    if (lot && lotDist(p) < k.r * 0.7 + 0.2) return false;
    if (doorPath(p)) return false;
    return !crowded(p, k.r, gap);
  };
  const innerCurve = p => {
    const q = nearestRoad(p, 12);
    if (!q || q.d > (layout.innerCurveReach ?? 8.5) || !isFinite(q.radius)) return false;
    const left = (q.dir[0] * (p[1] - q.p[1]) - q.dir[1] * (p[0] - q.p[0])) > 0;
    return Math.abs(q.radius) < 28 && (q.radius > 0) === left;
  };
  let featureFails = [];

  // 1. Fitur tetap dari layout.
  for (const f of layout.features ?? []) {
    if (f.kind === 'prop') {
      const ok = valid(f.sprite, f.at, { waterMin: f.onShore ? -3 : undefined, gap: 0.2, fixed: true });
      if (!ok) featureFails.push(`${f.sprite} (${f.at})`);
      else addProp({ kind: f.sprite, pos: f.at, fixed: true, flip: !!f.flip });
    } else if (f.kind === 'orchard') {
      const [x0, y0, x1, y1] = f.rect, sp = f.spacing ?? 4.6;
      for (let y = y0; y <= y1 + 1e-6; y += sp) for (let x = x0; x <= x1 + 1e-6; x += sp) {
        const p = [x + R.range(-0.4, 0.4), y + R.range(-0.4, 0.4)];
        const kind = f.sprite ?? 'pohon2';
        if (valid(kind, p, { gap: 0 }) && !innerCurve(p)) addProp({ kind, pos: p, fixed: true, group: 'kebun' });
      }
    } else if (f.kind === 'cluster') {
      for (let i = 0, placed = 0; i < 80 && placed < f.count; i++) {
        const a = R() * Math.PI * 2, rr = Math.sqrt(R()) * f.radius;
        const p = [f.center[0] + Math.cos(a) * rr, f.center[1] + Math.sin(a) * rr];
        const kind = R.weighted(f.sprites);
        if (valid(kind, p, { fixed: true })) { addProp({ kind, pos: p, fixed: true, group: f.group ?? 'kelompok' }); placed++; }
      }
    } else if (f.kind === 'row') {
      const total = dist(f.from, f.to), n = Math.max(1, Math.round(total / f.spacing));
      for (let i = 0; i <= n; i++) {
        const p = add(f.from, mul(sub(f.to, f.from), i / n));
        if (valid(f.sprite, p, { gap: 0 })) addProp({ kind: f.sprite, pos: p, fixed: true, group: 'deret' });
      }
    }
  }
  check('fitur-aman', 'Fitur tetap (lapak, taman, dermaga…) tidak di jalan/air/rumah', featureFails.length === 0, featureFails.length ? featureFails.join('; ') : 0, 0);

  // 1b. Hiasan pantai: pasir (0 < air < 0,35) tidak ditanami pohon/semak, jadi diberi batu dan bangku berjarak
  // `beachDecor.spacing` supaya layar pantai tidak kosong. Benih terpisah: urutan acak hiasan lain tidak bergeser.
  if (layout.beachDecor) {
    const BR = rng((layout.seed ?? 1) + 29), { spacing = 7, sprites = { batu: 1 } } = layout.beachDecor;
    for (let y = ext[1]; y <= ext[3]; y += 1.5) for (let x = ext[0]; x <= ext[2]; x += 1.5) {
      const p = [x + BR.range(-0.6, 0.6), y + BR.range(-0.6, 0.6)], w = water(p);
      if (w <= -0.4 || w >= 0.35) continue;
      const kind = BR.weighted(sprites);
      // Batu boleh setengah di air (tepi pantai berbatu); bangku selalu di pasir.
      const waterMin = kind === 'batu' ? -0.4 : 0.05;
      if (valid(kind, p, { waterMin, paveMin: PROP_KINDS[kind].r + 0.9, gap: spacing })) addProp({ kind, pos: p, fixed: true, group: 'pantai' });
    }
  }

  // 2. Lampu jalan: rapat di jalan utama, jarang di jalan kampung.
  net.edges.forEach((e, ei) => {
    let side = ei % 2 ? 1 : -1;
    const step = e.cls === 'utama' ? (layout.lampSpacing ?? 16) : (layout.lampSpacingKampung ?? 24);
    for (let s = 7; s < e.length - 5; s += step) {
      const { p, dir } = pointAt(e.poly, s);
      const pos = add(p, mul(perp(dir), side * (hw + 1.2)));
      side = -side;
      if (nearJunction(pos, 9)) continue;
      if (valid('lampu', pos, { paveMin: 0.7, gap: 1.5 })) addProp({ kind: 'lampu', pos, fixed: true, group: 'lampu' });
    }
  });
  // 2b. Sudut persimpangan: satu lampu di sudut pertama, bedeng bunga atau semak di sudut lain.
  const ends = nodeEnds(net);
  junctions.forEach((j, ji) => {
    const angs = ends[j.id].map(x => Math.atan2(x.dir[1], x.dir[0])).sort((a, b) => a - b);
    angs.forEach((a, i) => {
      const b = i + 1 < angs.length ? angs[i + 1] : angs[0] + Math.PI * 2;
      if (b - a > Math.PI * 1.1) return;
      const mid = (a + b) / 2, dir = [Math.cos(mid), Math.sin(mid)];
      let t = hw;
      while (t < 16 && pave(add(j.pos, mul(dir, t))) < 0.9) t += 0.2;
      const kind = i === ji % angs.length ? 'lampu' : (R() < 0.55 ? 'bedeng' : 'semak');
      const pos = add(j.pos, mul(dir, t + (kind === 'lampu' ? 0.3 : PROP_KINDS[kind].r + 0.5)));
      if (valid(kind, pos, { paveMin: kind === 'lampu' ? 0.7 : undefined, gap: 0.4 })) addProp({ kind, pos, fixed: kind === 'lampu', group: 'sudut' });
    });
  });

  // 3. Kotak surat tiap rumah, di sisi jalan setapak.
  houses.forEach(h => {
    for (const sgn of [1, -1]) {
      const pos = add(add(h.center, mul(h.n, -(HOUSE.halfD - 0.3))), mul(h.dir, sgn * 2.0));
      if (pave(pos) > 0.6 && water(pos) > 0.8 && !crowded(pos, 0.35, 0.2)) { addProp({ kind: 'kotakSurat', pos, fixed: true, group: 'rumah', house: h }); break; }
    }
  });

  // 3b. Tanaman tepi jalan: semak, bedeng bunga, dan batu kecil di bahu jalan supaya tiap layar tidak kosong.
  net.edges.forEach(e => {
    for (let s = 4; s < e.length - 4; s += layout.vergeSpacing ?? 6.5) {
      if (R() > (layout.vergeChance ?? 0.7)) continue;
      const { p, dir } = pointAt(e.poly, s);
      const side = R() < 0.5 ? 1 : -1;
      const pos = add(p, mul(perp(dir), side * (hw + 1.7 + R() * (layout.vergeDepth ?? 2.4))));
      if (nearJunction(pos, layout.vergeJunction ?? 7.5)) continue;
      const kind = lotDist(pos) < 3 ? (R() < 0.6 ? 'bedeng' : 'semak') : R.weighted({ semak: 5, bedeng: 2, batu: 2 });
      if (valid(kind, pos, { gap: 0.8 })) addProp({ kind, pos, group: 'tepi-jalan' });
    }
  });

  // 4. Pita pohon di tepi peta.
  const band = layout.treeBand ?? 9;
  const poisson = (rMin, accept) => {
    const pts = [], cell = rMin / Math.SQRT2, gw = Math.ceil((ext[2] - ext[0]) / cell), gh = Math.ceil((ext[3] - ext[1]) / cell);
    const grid = new Int32Array(gw * gh).fill(-1);
    const gi = p => Math.floor((p[1] - ext[1]) / cell) * gw + Math.floor((p[0] - ext[0]) / cell);
    const ok = p => {
      const cx = Math.floor((p[0] - ext[0]) / cell), cy = Math.floor((p[1] - ext[1]) / cell);
      for (let y = Math.max(0, cy - 2); y <= Math.min(gh - 1, cy + 2); y++) for (let x = Math.max(0, cx - 2); x <= Math.min(gw - 1, cx + 2); x++) {
        const k = grid[y * gw + x]; if (k >= 0 && dist(pts[k], p) < rMin) return false;
      }
      return true;
    };
    const active = [];
    const seed = [R.range(ext[0], ext[2]), R.range(ext[1], ext[3])];
    pts.push(seed); grid[gi(seed)] = 0; active.push(0);
    while (active.length) {
      const ai = R.int(active.length), base = pts[active[ai]];
      let found = false;
      for (let t = 0; t < 24; t++) {
        const a = R() * Math.PI * 2, rr = rMin * (1 + R());
        const p = [base[0] + Math.cos(a) * rr, base[1] + Math.sin(a) * rr];
        if (p[0] < ext[0] || p[0] >= ext[2] || p[1] < ext[1] || p[1] >= ext[3] || !ok(p)) continue;
        pts.push(p); grid[gi(p)] = pts.length - 1; active.push(pts.length - 1); found = true; break;
      }
      if (!found) active.splice(ai, 1);
    }
    return pts.filter(accept);
  };
  const edgeDist = p => Math.min(p[0] - ext[0], ext[2] - p[0], p[1] - ext[1], ext[3] - p[1]);
  for (const p of poisson(3.4, p => edgeDist(p) < band)) {
    const kind = R() < 0.55 ? 'pohon' : 'pohon2';
    if (valid(kind, p, { gap: -0.4 })) addProp({ kind, pos: p, group: 'tepi' });
  }

  // 4b. Hutan: lahan yang jauh dari jalan (tidak pernah dilewati) diisi pohon rapat supaya kota terasa di tengah hutan, bukan padang kosong.
  // (SDF aspal dipotong di ~5 unit, jadi jarak jauh diukur dari indeks titik jalan.)
  const forestFrom = layout.forestFrom ?? 15;
  const deep = p => { const q = nearestRoad(p, forestFrom + hw + 3); return !q || q.d - hw > forestFrom; };
  for (const p of poisson(layout.forestSpacing ?? 3.8, p => edgeDist(p) >= band && deep(p))) {
    const kind = R() < 0.8 ? (R() < 0.5 ? 'pohon' : 'pohon2') : 'semak';
    if (valid(kind, p, { gap: -0.3 })) addProp({ kind, pos: p, group: 'hutan' });
  }

  // 5. Isian alami: rumpun pohon, semak, batu, dan bedeng bunga di pekarangan.
  const grove = noise2((layout.seed ?? 1) + 7, layout.decor?.groveScale ?? 22, 2);
  const spacing = layout.decor?.spacing ?? 4.4;
  for (const p of poisson(spacing, p => edgeDist(p) >= band && !deep(p))) {
    const di = districtOf(p), dd = layout.districts[di].decor ?? {};
    const g = grove(p[0], p[1]);
    const nearLot = lotDist(p) < 2.6;
    let kind;
    if (nearLot) kind = R() < (dd.flowers ?? 0.45) ? 'bedeng' : 'semak';
    else if (g > (dd.treeAbove ?? layout.decor?.treeAbove ?? 0.55)) kind = R() < 0.5 ? 'pohon' : 'pohon2';
    else if (g > (dd.bushAbove ?? layout.decor?.bushAbove ?? 0.42)) kind = R() < (dd.rocks ?? 0.25) ? 'batu' : 'semak';
    else if (R() < (dd.sparse ?? layout.decor?.sparse ?? 0.25)) kind = R() < (dd.rocks ?? 0.25) + 0.2 ? 'batu' : 'semak';
    else continue;
    if (PROP_KINDS[kind].tree && (nearJunction(p, 12) || innerCurve(p) || pave(p) < 2.2)) kind = 'semak';
    if (valid(kind, p)) addProp({ kind, pos: p, group: 'isian' });
  }

  // 6. Aturan sel 12×12: tiap sel rumput minimal satu fitur, maksimal 9 properti.
  const C = 12;
  const cells = [];
  const g = fields.grid;
  for (let cy = ext[1]; cy < ext[3] - 1e-6; cy += C) for (let cx = ext[0]; cx < ext[2] - 1e-6; cx += C) {
    let grass = 0, wet = 0, tot = 0;
    for (let y = cy; y < cy + C; y += 1) for (let x = cx; x < cx + C; x += 1) {
      const p = [x + 0.5, y + 0.5]; tot++;
      const w = water(p);  // pasir pantai (0 ≤ w < 0,35) bukan rumput
      if (w < 0) wet++; else if (w >= 0.35 && pave(p) > 0) grass++;
    }
    cells.push({ x: cx, y: cy, grass: grass / tot, wet: wet / tot });
  }
  const inCell = (c, p) => p[0] >= c.x && p[0] < c.x + C && p[1] >= c.y && p[1] < c.y + C;
  // Gudang depot juga fitur (bangunan terbesar di kota).
  const bld = layout.depot?.building;
  const hasDepot = c => !!bld && Math.abs(c.x + C / 2 - bld.center[0]) < (C + bld.size[0]) / 2 && Math.abs(c.y + C / 2 - bld.center[1]) < (C + bld.size[1]) / 2;
  let filled = 0;
  for (const c of cells) {
    if (c.grass < 0.5) continue;
    const has = props.some(pr => inCell(c, pr.pos)) || houses.some(h => inCell(c, h.center)) || c.wet > 0.1 || hasDepot(c);
    if (has) continue;
    for (let t = 0; t < 200; t++) {
      const p = [c.x + R() * C, c.y + R() * C];
      const kind = t < 100 ? (R() < 0.6 ? 'semak' : 'batu') : 'batu';
      if (valid(kind, p)) { addProp({ kind, pos: p, group: 'isian' }); filled++; break; }
    }
  }
  const removable = ['batu', 'semak', 'bedeng', 'pohon2', 'pohon'];
  let trimmed = 0;
  for (const c of cells) {
    let inside_ = props.filter(pr => inCell(c, pr.pos));
    while (inside_.length > 9) {
      const victim = removable.map(k => inside_.find(pr => pr.kind === k && !pr.fixed)).find(Boolean);
      if (!victim) break;
      props.splice(props.indexOf(victim), 1); trimmed++;
      inside_ = inside_.filter(x => x !== victim);
    }
  }
  // 7. Isian layar: kalau layar di satu titik jalan berisi kurang dari 4 objek, tanam semak/bedeng/batu di bahu jalan dalam layar itu.
  const roadSamples = net.edges.flatMap(e => resample(e.poly, 5));
  const screenCount = (p, w, h) => {
    const within = q => Math.abs(q[0] - p[0]) < w / 2 && Math.abs(q[1] - p[1]) < h / 2;
    return props.filter(pr => within(pr.pos)).length + houses.filter(hh => within(hh.center)).length;
  };
  let screenFilled = 0;
  for (const [w, h] of Object.values(SCREENS)) for (const p of roadSamples) {
    for (let t = 0; t < 600 && screenCount(p, w, h) < 4; t++) {
      const q = [p[0] + (R() - 0.5) * (w - 2), p[1] + (R() - 0.5) * (h - 2)];
      const pv = pave(q);
      if (pv < 1.4 || pv > 9) continue;
      // Di pasir pantai hanya batu (tanaman tidak tumbuh di pasir).
      const sand = water(q) < 0.35;
      const pick = R.weighted({ semak: 4, bedeng: 3, batu: 2 }), kind = sand ? 'batu' : pick;  // R tetap dipakai: urutan acak sama
      const cell = cells.find(c => inCell(c, q));
      if (cell && props.filter(pr => inCell(cell, pr.pos)).length >= 9) continue;
      if (valid(kind, q, { gap: 0.8, waterMin: sand ? 0.05 : undefined })) { addProp({ kind, pos: q, group: 'isian-layar' }); screenFilled++; }
    }
  }
  const emptyCells = cells.filter(c => c.grass >= 0.5 && !props.some(pr => inCell(c, pr.pos)) && !houses.some(h => inCell(c, h.center)) && c.wet <= 0.1 && !hasDepot(c));
  const busyCells = cells.filter(c => props.filter(pr => inCell(c, pr.pos)).length > 9);
  check('sel-kosong', 'Sel rumput 12×12 tanpa satu fitur pun', emptyCells.length === 0, emptyCells.length ? `${emptyCells.length} (${emptyCells.slice(0, 4).map(c => `${c.x},${c.y}`).join('; ')})` : 0, 0);
  check('sel-padat', 'Sel 12×12 dengan lebih dari 9 properti', busyCells.length === 0, busyCells.length, 0);

  // Aturan layar: dihitung dari sudut pandang truk di jalan (kamera mengikuti truk).
  const screens = {};
  for (const [name, [w, h]] of Object.entries(SCREENS)) {
    let lo = Infinity, hi = 0, loAt = null, hiAt = null;
    for (const p of roadSamples) {
      const n = screenCount(p, w, h);
      if (n < lo) { lo = n; loAt = p; }
      if (n > hi) { hi = n; hiAt = p; }
    }
    screens[name] = { min: lo, max: hi, minAt: loAt, maxAt: hiAt };
    check(`layar-${name}`, `Objek (properti + rumah) per layar ${name} (${w}×${h})`, lo >= 4 && hi <= 30, `${lo}–${hi} (tersepi di ${Math.round(loAt[0])},${Math.round(loAt[1])})`, '4–30');
  }
  const treesBad = props.filter(pr => PROP_KINDS[pr.kind].tree && pr.group !== 'kebun' && (innerCurve(pr.pos) || pave(pr.pos) < 2.2)).length;
  check('pohon-tikungan', 'Pohon di tikungan dalam atau terlalu dekat jalan', treesBad === 0, treesBad, 0);
  // Pita pohon: tiap 12 unit keliling peta punya minimal satu pohon di pita tepi.
  let bandGaps = 0;
  const per = [];
  for (let x = ext[0]; x < ext[2]; x += 12) per.push([[x, ext[1]], [x + 12, ext[1] + band]], [[x, ext[3] - band], [x + 12, ext[3]]]);
  for (let y = ext[1]; y < ext[3]; y += 12) per.push([[ext[0], y], [ext[0] + band, y + 12]], [[ext[2] - band, y], [ext[2], y + 12]]);
  for (const [a, b] of per) {
    const hasTree = props.some(pr => PROP_KINDS[pr.kind].tree && pr.pos[0] >= a[0] && pr.pos[0] <= b[0] && pr.pos[1] >= a[1] && pr.pos[1] <= b[1]);
    let road = false;
    for (let y = a[1]; y <= b[1] && !road; y += 1) for (let x = a[0]; x <= b[0] && !road; x += 1) if (pave([x, y]) < 1) road = true;
    let wet = 0;
    for (let y = a[1]; y <= b[1]; y += 2) for (let x = a[0]; x <= b[0]; x += 2) if (water([x, y]) < 1) wet++;
    if (!hasTree && !road && wet === 0) bandGaps++;   // tepi laut tidak butuh pohon
  }
  check('pita-pohon', 'Potongan tepi peta tanpa pohon', bandGaps === 0, bandGaps, 0);
  const onRoad = props.filter(pr => pave(pr.pos) < PROP_KINDS[pr.kind].r * 0.5).length;
  check('properti-jalan', 'Properti menyentuh aspal', onRoad === 0, onRoad, 0);

  // ---------- titik singgah jalanan (bantuan warga, cap singgah, bantuan cepat tambahan) ----------
  // Di lajur kiri (sisi kiri arah ruas), jauh dari persimpangan, zona antar rumah, dan halaman depot.
  const activityStops = [];
  net.edges.forEach((e, ei) => {
    const gap = layout.activityStopSpacing ?? 26;
    for (let s0 = 12; s0 < e.length - 12; s0 += 2) {
      const { p, dir } = pointAt(e.poly, s0);
      const pos = add(p, mul(perp(dir), HOUSE.stopOffset));
      if (nearJunction(pos, 14) || plaza(pos) < 6 || pave(pos) > -1.2) continue;
      if (houses.some(h => dist(h.stop, pos) < 8) || activityStops.some(a => dist(a.pos, pos) < gap)) continue;
      activityStops.push({ pos, ei, angle: Math.atan2(dir[1], dir[0]) * 180 / Math.PI - 90 });
    }
  });
  check('titik-singgah', 'Titik singgah jalanan (bantuan warga, cap, kilat tambahan)', activityStops.length >= 8, activityStops.length, '≥ 8');

  // ---------- marka ----------
  const skipR = hw + (layout.fillet ?? 2.6) + 2;
  const marks = [];
  return { houses, props, marks, checks, screens, skipR, activityStops, cells, lotDist, pave, water, plaza, nearJunction, filled, trimmed, screenFilled, nearestRoad };
}

function segDistSimple(p, a, b) {
  const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy || 1e-9;
  let t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2; t = Math.max(0, Math.min(1, t));
  return Math.hypot(p[0] - a[0] - dx * t, p[1] - a[1] - dy * t);
}

// Marka putus-putus di tengah goresan, berhenti sebelum persimpangan dan pelataran.
export function bakeMarkings(strokes, junctions, plaza, skipR) {
  const dashes = [];
  const DASH = 1.3, GAP = 1.1, STEP = 0.1;
  for (const s of strokes) {
    const pts = resample(s.poly, STEP, s.closed);
    if (s.closed) pts.push(pts[0]);
    let phase = 0, cur = null;
    const ok = p => plaza(p) > 1.5 && junctions.every(j => dist(p, j.pos) > skipR);
    for (let i = 0; i < pts.length; i++) {
      const on = (phase % (DASH + GAP)) < DASH;
      if (on && ok(pts[i])) { if (!cur) cur = [pts[i], pts[i]]; else cur[1] = pts[i]; }
      else if (cur) { if (dist(cur[0], cur[1]) > 0.6) dashes.push(cur); cur = null; }
      phase += STEP;
    }
    if (cur && dist(cur[0], cur[1]) > 0.6) dashes.push(cur);
  }
  return dashes;
}

// Jarak tempuh terpendek dari mulut depot ke setiap titik berhenti rumah (Dijkstra pada graf ruas).
export function routeStats(net, houses, from, speed = 6.5) {
  const ids = Object.keys(net.nodes);
  const nearestEdge = p => {
    let best = null;
    net.edges.forEach((e, ei) => e.poly.forEach((q, k) => { const d = dist(p, q); if (!best || d < best.d) best = { d, ei, s: k * 0.25 }; }));
    return best;
  };
  const start = nearestEdge(from);
  const d0 = Object.fromEntries(ids.map(id => [id, Infinity]));
  const e0 = net.edges[start.ei];
  d0[e0.from] = Math.min(d0[e0.from], start.s);
  d0[e0.to] = Math.min(d0[e0.to], e0.length - start.s);
  const done = new Set();
  while (done.size < ids.length) {
    let u = null; for (const id of ids) if (!done.has(id) && (u === null || d0[id] < d0[u])) u = id;
    if (u === null || d0[u] === Infinity) break;
    done.add(u);
    for (const e of net.edges) {
      if (e.from === u && d0[u] + e.length < d0[e.to]) d0[e.to] = d0[u] + e.length;
      if (e.to === u && d0[u] + e.length < d0[e.from]) d0[e.from] = d0[u] + e.length;
    }
  }
  const perHouse = houses.map(h => {
    const e = net.edges[h.ei];
    let d = Math.min(d0[e.from] + h.s, d0[e.to] + e.length - h.s);
    if (h.ei === start.ei) d = Math.min(d, Math.abs(h.s - start.s));
    return d;
  });
  const lens = [...perHouse].sort((a, b) => a - b);
  const med = lens[Math.floor(lens.length / 2)] ?? 0, max = lens[lens.length - 1] ?? 0;
  return { median: med, max, mean: lens.reduce((a, b) => a + b, 0) / (lens.length || 1), seconds: { median: med / speed, max: max / speed }, speed, perHouse: perHouse.map(d => Math.round(d * 10) / 10) };
}

// Bobot peluang rumah tujuan. Tiga faktor dikali lalu dinormalkan:
//   jarak     f = 1 / (1 + a·(d/median)²)       rumah jauh lebih jarang, tapi tidak pernah nol
//   kepadatan g = 1 / (1 + n/k)                 n = rumah lain dalam radius R; kawasan padat tidak mendominasi
//   w = f·g, dinormalkan ke rata-rata 1, dijepit ke [lo, hi], lalu p = w / Σw.
export function deliveryWeights(houses, routeLen, opts = {}) {
  const a = opts.distance ?? 0.8, k = opts.k ?? 3, R = opts.radius ?? 22, lo = opts.min ?? 0.35, hi = opts.max ?? 2.4;
  const sorted = [...routeLen].sort((x, y) => x - y);
  const med = sorted[Math.floor(sorted.length / 2)] || 1;
  const raw = houses.map((h, i) => {
    const n = houses.filter((o, j) => j !== i && dist(o.center, h.center) < R).length;
    const f = 1 / (1 + a * (routeLen[i] / med) ** 2), g = 1 / (1 + n / k);
    return { n, f, g, w: f * g };
  });
  const mean = raw.reduce((s, x) => s + x.w, 0) / (raw.length || 1);
  const w = raw.map(x => Math.min(hi, Math.max(lo, x.w / mean)));
  const sum = w.reduce((s, x) => s + x, 0) || 1;
  const p = w.map(x => x / sum);
  // Ringkasan: bagian peluang per sepertiga jarak (dekat/sedang/jauh) dibanding bagian jumlah rumah.
  const order = routeLen.map((d, i) => i).sort((x, y) => routeLen[x] - routeLen[y]);
  const terciles = [0, 1, 2].map(t => {
    const idx = order.slice(Math.round(t * order.length / 3), Math.round((t + 1) * order.length / 3));
    return { name: ['dekat', 'sedang', 'jauh'][t], from: routeLen[idx[0]], to: routeLen[idx[idx.length - 1]], houses: idx.length,
      share: idx.reduce((s, i) => s + p[i], 0) };
  });
  const expected = p.reduce((s, x, i) => s + x * routeLen[i], 0);
  const uniform = routeLen.reduce((s, x) => s + x, 0) / (routeLen.length || 1);
  return { p, raw, median: med, params: { a, k, R, lo, hi }, terciles, expected, uniform,
    ratio: Math.max(...p) / Math.min(...p), minP: Math.min(...p), maxP: Math.max(...p) };
}

export { deg };
