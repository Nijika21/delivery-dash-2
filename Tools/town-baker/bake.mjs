// Baker kota Delivery Dash 2: layout JSON tulisan tangan -> data bake yang dibaca Unity dan pratinjau HTML.
// Pemakaian:
//   node Tools/town-baker/bake.mjs                 # bake semua peta ke Temp/peta/<id>/
//   node Tools/town-baker/bake.mjs kota-paket-2    # satu peta
//   node Tools/town-baker/bake.mjs --check         # hanya periksa, keluar dengan kode 1 kalau ada aturan gagal
//   node Tools/town-baker/bake.mjs --png <file>    # gambar cek cepat (untuk satu peta)
//   node Tools/town-baker/bake.mjs --out <folder>  # folder keluaran lain (mis. Assets/World/... saat I3)
import { readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { buildNetwork, validateNetwork, buildStrokes } from './lib/graph.mjs';
import { expandGrid } from './lib/grid.mjs';
import { computeFields, bakeColliders, encodeSdf, encodeGround, sampler, CELL, wallGrid } from './lib/field.mjs';
import { placeTown, bakeMarkings, routeStats, deliveryWeights, districtLocator, PROP_KINDS, HOUSE } from './lib/place.mjs';
import { noise2 } from './lib/rng.mjs';
import { dist, pt3, round3, resample, add, mul, perp, pointAt, nearestOnPoly } from './lib/geom.mjs';
import { renderDebug, renderMinimap } from './lib/raster.mjs';
import { encodePng } from '../art/png.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = join(HERE, '..', '..');
const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
const args = isMain ? process.argv.slice(2) : [];
const flag = name => { const i = args.indexOf(name); return i >= 0 ? (args.splice(i, 2)[1] ?? true) : null; };
const checkOnly = args.includes('--check'); if (checkOnly) args.splice(args.indexOf('--check'), 1);
const pngOut = flag('--png');
const outRoot = flag('--out') ?? join(ROOT, 'Temp/peta');
const maps = args.length ? args : readdirSync(join(HERE, 'maps')).filter(f => f.endsWith('.layout.json')).map(f => f.replace('.layout.json', ''));

export function bake(id) {
  const t0 = Date.now();
  const src = readFileSync(join(HERE, 'maps', `${id}.layout.json`), 'utf8');
  const layout = expandGrid(JSON.parse(src));
  const net = buildNetwork(layout);
  // Jalan masuk depot: ujungnya ditempel ke garis tengah jalan yang disebut; mulut jalan masuk = titik keluar depot.
  const dw = layout.depot?.driveway;
  if (dw?.road) {
    const e = net.edges.find(x => x.id === dw.road);
    if (!e) throw new Error(`Jalan masuk depot: jalan ${dw.road} tidak ada`);
    dw.to = nearestOnPoly(e.poly, dw.to).p;
    if (!layout.depot.exit) layout.depot.exit = dw.to;
  }
  const graph = validateNetwork(net, layout);
  const strokes = buildStrokes(net);
  const fields = computeFields(layout, net, strokes);
  const building = layout.depot?.building;
  const solids = building ? [[building.center[0] - building.size[0] / 2, building.center[1] - building.size[1] / 2,
    building.center[0] + building.size[0] / 2, building.center[1] + building.size[1] / 2]] : [];
  const colliders = bakeColliders(fields, solids);
  const town = placeTown(layout, net, fields, graph.junctions);
  const pave = sampler(fields.grid, fields.pave), water = sampler(fields.grid, fields.water), plaza = sampler(fields.grid, fields.plaza);
  const marks = bakeMarkings(strokes, graph.junctions, plaza, town.skipR);
  const checks = [...graph.checks, ...town.checks];
  const check = (cid, label, pass, value, limit) => checks.push({ id: cid, label, pass, value, limit });

  // Muka dinding tabrakan (sisi sel tertutup yang berbatasan dengan sel terbuka) jatuh di pita pinggir jalan:
  // tidak di aspal hitam (SDF < -0,05) dan tidak melewati pita abu ke rumput (SDF > 0,5; tepi luar pita abu 0,48).
  {
    const g = fields.grid, walls = wallGrid(fields, solids);
    const closed = (i, j) => i < 0 || j < 0 || i >= g.W || j >= g.H || walls[j * g.W + i] === 1;
    let lo = Infinity, hi = -Infinity;
    for (let j = 0; j < g.H; j++) for (let i = 0; i < g.W; i++) {
      if (closed(i, j)) continue;
      const cx = g.x0 + (i + 0.5) * CELL, cy = g.y0 + (j + 0.5) * CELL;
      for (const [di, dj] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        if (!closed(i + di, j + dj)) continue;
        const v = pave([cx + di * CELL * 0.5, cy + dj * CELL * 0.5]);
        lo = Math.min(lo, v); hi = Math.max(hi, v);
      }
    }
    check('dinding-pita', 'Muka dinding tabrakan di pita pinggir jalan (SDF)', lo >= -0.05 && hi <= 0.5,
      `${lo.toFixed(2)} s.d. ${hi.toFixed(2)}`, '-0,05 s.d. 0,5');
  }

  // Danau tidak menyentuh jalan.
  let shore = Infinity;
  for (const l of fields.lakes) for (const p of resample(l.poly, 0.5, true)) shore = Math.min(shore, pave(p));
  if (fields.lakes.length) check('danau-jalan', 'Jarak tepi danau ke aspal', shore >= 2, shore.toFixed(1), '≥ 2');

  // Marka tidak masuk persimpangan.
  const badMarks = marks.filter(([a, b]) => graph.junctions.some(j => dist(a, j.pos) < town.skipR - 0.01 || dist(b, j.pos) < town.skipR - 0.01)).length;
  check('marka', 'Marka di dalam area persimpangan', badMarks === 0, badMarks, 0);

  // Anchor depot.
  const d = layout.depot;
  const anchors = {};
  if (d) {
    const safe = (p, lim) => pave(p) <= lim;
    const [fw, fh] = d.finish.size, fa = (d.finish.angle ?? 0) * Math.PI / 180;
    const corners = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([sx, sy]) => {
      const x = sx * fw / 2, y = sy * fh / 2;
      return [d.finish.center[0] + x * Math.cos(fa) - y * Math.sin(fa), d.finish.center[1] + x * Math.sin(fa) + y * Math.cos(fa)];
    });
    const b = d.building;
    let buildingOnRoad = 0;
    for (let y = -0.5; y <= 0.5; y += 0.1) for (let x = -0.5; x <= 0.5; x += 0.1) {
      const p = [b.center[0] + x * (b.size[0] - 0.8), b.center[1] + y * (b.size[1] - 0.8)];
      if (pave(p) < 0) buildingOnRoad++;
    }
    const anchorOk = safe(d.pickup, -1.5) && safe(d.start.pos, -1.2) && corners.every(c => safe(c, -0.3)) && safe(d.letterDrop, -1.2) && safe(d.exit, -1.5) && buildingOnRoad === 0;
    check('anchor', 'Anchor depot (ambil, mulai, selesai kerja, surat, gudang) aman', anchorOk,
      anchorOk ? 'aman' : `ambil ${pave(d.pickup).toFixed(1)}, mulai ${pave(d.start.pos).toFixed(1)}, kapsul ${Math.max(...corners.map(pave)).toFixed(1)}, surat ${pave(d.letterDrop).toFixed(1)}, gudang ${buildingOnRoad}`, 'aman');
    // Parkiran tidak menempel ke jalan: hanya tersambung lewat jalan masuk.
    if (d.driveway) {
      const [cx, cy] = d.plaza.center, [w, h] = d.plaza.size;
      let gap = Infinity, at = null;
      for (let t = 0; t < 1; t += 0.01) for (const [x, y] of [[cx - w / 2 + w * t, cy - h / 2], [cx - w / 2 + w * t, cy + h / 2], [cx - w / 2, cy - h / 2 + h * t], [cx + w / 2, cy - h / 2 + h * t]]) {
        const q = town.nearestRoad([x, y], 40), g_ = q ? q.d - net.halfWidth : 40;
        if (g_ < gap) { gap = g_; at = [x, y]; }
      }
      check('parkir-jalan', 'Jarak tepi parkiran depot ke aspal jalan (hanya lewat jalan masuk)', gap >= 3, `${gap.toFixed(1)} (${Math.round(at[0])},${Math.round(at[1])})`, '≥ 3');
      const lotArea = w * h;
      check('parkir-luas', 'Luas parkiran depot', lotArea <= 240, `${w}×${h} = ${lotArea}`, '≤ 240');
    }
    Object.assign(anchors, {
      pickup: pt3(d.pickup), start: { pos: pt3(d.start.pos), heading: d.start.heading },
      finish: { center: pt3(d.finish.center), size: d.finish.size, angle: d.finish.angle ?? 0 },
      warehouse: { center: b.center, size: b.size }, letterDrop: pt3(d.letterDrop), exit: pt3(d.exit),
      plaza: d.plaza, ...(d.driveway ? { driveway: { from: pt3(d.driveway.from), to: pt3(d.driveway.to), width: d.driveway.width, road: d.driveway.road } } : {}),
    });
  }

  // Medan kawasan dan nada rumput.
  const g = fields.grid;
  const locate = districtLocator(layout);
  const tone = noise2((layout.seed ?? 1) + 3, 26, 3);
  const districtArr = new Uint8Array(g.nx * g.ny), toneArr = new Uint8Array(g.nx * g.ny);
  for (let j = 0; j < g.ny; j++) for (let i = 0; i < g.nx; i++) {
    const p = [g.x0 + i * CELL, g.y0 + j * CELL], di = locate(p);
    districtArr[j * g.nx + i] = di;
    toneArr[j * g.nx + i] = Math.max(0, Math.min(255, Math.round(tone(p[0], p[1]) * 255 + (layout.districts[di].tone ?? 0))));
  }

  const routes = d ? routeStats(net, town.houses, d.exit) : null;
  const weights = routes ? deliveryWeights(town.houses, routes.perHouse, layout.weights) : null;
  if (weights) {
    const shares = weights.terciles.map(t => t.share);
    const okShare = shares.every(x => x >= 0.2 && x <= 0.46) && shares[0] >= shares[2];
    check('peluang', 'Peluang antar per sepertiga jarak (dekat/sedang/jauh)', okShare && weights.ratio <= 8,
      `${shares.map(x => Math.round(x * 100) + '%').join(' / ')}, rasio ${weights.ratio.toFixed(1)}×`, 'tiap 20–46%, dekat ≥ jauh, rasio ≤ 8×');
  }
  const pass = checks.every(c => c.pass);
  const hash = createHash('sha256').update(src).digest('hex').slice(0, 16);
  const houses = town.houses.map((h, i) => ({
    id: `r${String(i + 1).padStart(2, '0')}`, name: h.name, district: layout.districts[h.district].id, type: h.type, colorway: h.colorway,
    center: pt3(h.center), angle: round3(Math.atan2(h.n[1], h.n[0]) * 180 / Math.PI - 90),
    door: pt3(add(h.center, mul(h.n, -HOUSE.doorOffset))), stop: pt3(h.stop), road: net.edges[h.ei].id, tutorial: !!h.tutorial,
    ...(weights ? { weight: Math.round(weights.p[i] * 1e5) / 1e5, route: routes.perHouse[i] } : {}),
  }));
  const baked = {
    format: 'delivery-dash-town', version: 1, id: layout.id, name: layout.name, layoutHash: hash,
    extent: layout.extent, roadWidth: net.halfWidth * 2, fillet: layout.fillet ?? 2.6,
    field: {
      cell: CELL, origin: [g.x0, g.y0], size: [g.nx, g.ny], samples: 'titik kisi origin + (i, j) * cell, baris 0 = y terkecil',
      pavement: { file: 'pavement-sdf.bytes', type: 'int16le', scale: 256, note: 'negatif = di atas aspal/pelataran' },
      ground: { file: 'ground-field.bytes', type: 'rgba8', channels: ['kawasan', 'air', 'pelataran', 'nada'], quant: '128 + jarak * 16' },
    },
    districts: layout.districts.map(x => ({ id: x.id, name: x.name, center: x.center })),
    nodes: Object.values(net.nodes).map(n => ({ id: n.id, pos: pt3(n.pos), junction: graph.junctions.some(j => j.id === n.id) })),
    roads: net.edges.map(e => ({ id: e.id, name: e.name, class: e.cls, from: e.from, to: e.to, length: round3(e.length), points: resample(e.poly, 1).map(pt3) })),
    markings: marks.map(([a, b]) => [...pt3(a), ...pt3(b)]),
    lakes: fields.lakes.map(l => ({ id: l.id, name: l.name, shore: resample(l.poly, 0.5, true).map(pt3) })),
    anchors, houses,
    activityStops: town.activityStops.map(a => ({ pos: pt3(a.pos), road: net.edges[a.ei].id, angle: round3(a.angle) })),
    props: town.props.map(p => ({ kind: p.kind, pos: pt3(p.pos), height: PROP_KINDS[p.kind].h, ...(p.flip ? { flip: true } : {}) })),
    colliders,
  };
  const report = {
    id: layout.id, name: layout.name, layoutHash: hash, pass, bakeMs: Date.now() - t0,
    counts: {
      houses: houses.length, perDistrict: Object.fromEntries(layout.districts.map(x => [x.name, houses.filter(h => h.district === x.id).length])),
      props: baked.props.length, perKind: baked.props.reduce((m, p) => (m[p.kind] = (m[p.kind] ?? 0) + 1, m), {}),
      roads: net.edges.length, junctions: graph.junctions.length, roadLength: Math.round(net.edges.reduce((s, e) => s + e.length, 0)),
      colliders: colliders.length, markings: marks.length, fieldSize: [g.nx, g.ny],
    },
    screens: town.screens, routes, junctions: graph.junctions, checks,
    weights: weights && { params: weights.params, median: weights.median, terciles: weights.terciles, expected: weights.expected, uniform: weights.uniform,
      ratio: weights.ratio, minP: weights.minP, maxP: weights.maxP,
      perDistrict: Object.fromEntries(layout.districts.map((x, di) => [x.name, town.houses.reduce((s, h, i) => s + (h.district === di ? weights.p[i] : 0), 0)])) },
  };
  return { layout, net, strokes, fields, baked, report, town, graph, districtArr, toneArr };
}

// JsonUtility tidak membaca tuple `[x,y]`. Salinan ini mempertahankan data bake
// yang sama, tetapi memakai objek bernama agar loader Unity tetap kecil dan AOT-safe.
function unityJson(b) {
  const v = p => ({ x: p?.[0] ?? 0, y: p?.[1] ?? 0 });
  const box = r => ({ x: r?.[0] ?? 0, y: r?.[1] ?? 0, width: r?.[2] ?? 0, height: r?.[3] ?? 0 });
  const anchors = b.anchors ?? {};
  return {
    format: b.format, version: b.version, id: b.id, name: b.name, layoutHash: b.layoutHash,
    extent: { xMin: b.extent[0], yMin: b.extent[1], xMax: b.extent[2], yMax: b.extent[3] },
    roadWidth: b.roadWidth,
    field: { x0: b.field.origin[0], y0: b.field.origin[1], cell: b.field.cell, nx: b.field.size[0], ny: b.field.size[1] },
    anchors: {
      pickup: v(anchors.pickup),
      start: { pos: v(anchors.start?.pos), heading: anchors.start?.heading ?? 0 },
      finish: { center: v(anchors.finish?.center), size: v(anchors.finish?.size), angle: anchors.finish?.angle ?? 0 },
      warehouse: { center: v(anchors.warehouse?.center), size: v(anchors.warehouse?.size) },
      letterDrop: v(anchors.letterDrop), exit: v(anchors.exit),
      plaza: { center: v(anchors.plaza?.center), size: v(anchors.plaza?.size), radius: anchors.plaza?.radius ?? 0 },
      driveway: { from: v(anchors.driveway?.from), to: v(anchors.driveway?.to), width: anchors.driveway?.width ?? 0, road: anchors.driveway?.road ?? '' },
    },
    roads: b.roads.map(r => ({ id: r.id, name: r.name, roadClass: r.class, from: r.from, to: r.to, length: r.length, points: r.points.map(v) })),
    houses: b.houses.map(h => ({ ...h, center: v(h.center), door: v(h.door), stop: v(h.stop) })),
    activityStops: b.activityStops.map(a => ({ pos: v(a.pos), road: a.road, angle: a.angle })),
    props: b.props.map(p => ({ kind: p.kind, pos: v(p.pos), height: p.height, flip: !!p.flip })),
    lakes: b.lakes.map(l => ({ id: l.id, name: l.name, shore: l.shore.map(v) })),
    markings: b.markings.map(m => ({ x0: m[0], y0: m[1], x1: m[2], y1: m[3] })),
    colliders: b.colliders.map(box),
  };
}

let failed = false;
for (const id of isMain ? maps : []) {
  const r = bake(id);
  const { report } = r;
  console.log(`\n${report.name} (${id}) — ${report.pass ? 'LOLOS' : 'GAGAL'} dalam ${report.bakeMs} ms`);
  for (const c of report.checks) console.log(`  ${c.pass ? '✓' : '✗'} ${c.label}: ${c.value} (batas ${c.limit})`);
  console.log(`  rumah ${report.counts.houses} ${JSON.stringify(report.counts.perDistrict)}, properti ${report.counts.props}, collider ${report.counts.colliders}`);
  if (report.routes) console.log(`  rute dari depot: median ${report.routes.median.toFixed(0)} (${report.routes.seconds.median.toFixed(1)} dtk), terjauh ${report.routes.max.toFixed(0)} (${report.routes.seconds.max.toFixed(1)} dtk)`);
  if (!report.pass) failed = true;
  if (pngOut) { writeFileSync(pngOut, encodePng(renderDebug(r, 3))); console.log(`  gambar cek: ${pngOut}`); }
  if (checkOnly) continue;
  const out = join(outRoot, id);
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, 'town.baked.json'), JSON.stringify(r.baked));
  writeFileSync(join(out, 'town.unity.json'), JSON.stringify(unityJson(r.baked)));
  writeFileSync(join(out, 'report.json'), JSON.stringify(report, null, 2));
  writeFileSync(join(out, 'pavement-sdf.bytes'), encodeSdf(r.fields.pave));
  writeFileSync(join(out, 'ground-field.bytes'), encodeGround(r.fields, r.districtArr, r.toneArr));
  writeFileSync(join(out, 'minimap.png'), encodePng(renderMinimap(r, 2)));
  // UI: minimap kecil tampil 3,2 px/unit (skala 1,6 × 2); versi 2x untuk layar HP.
  writeFileSync(join(out, 'minimap-ui.png'), encodePng(renderMinimap(r, 6.4, { detail: true })));
  console.log(`  keluaran: ${out}`);
}
if (isMain) process.exitCode = failed ? 1 : 0;
