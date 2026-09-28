// Jaringan jalan: simpul, ruas, validasi graf, dan "goresan" (rangkaian ruas yang menyambung lurus).
import { add, sub, mul, dot, dist, norm, perp, resample, catmullRom, roundedPolyline, superellipse,
  nearestOnPoly, polyLength, pointAt, curvatureProfile, segDist } from './geom.mjs';

const STEP = 0.25;

// Potong polyline tertutup dari panjang busur s0 sampai s1 (s1 boleh melewati total = memutar).
function ringSlice(poly, s0, s1) {
  const cum = [0];
  for (let i = 1; i <= poly.length; i++) cum.push(cum[i - 1] + dist(poly[i - 1], poly[i % poly.length]));
  const total = cum[poly.length];
  const at = s => {
    s = ((s % total) + total) % total;
    let i = 0; while (cum[i + 1] < s) i++;
    const t = (s - cum[i]) / ((cum[i + 1] - cum[i]) || 1);
    const a = poly[i], b = poly[(i + 1) % poly.length];
    return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
  };
  const out = [at(s0)];
  for (let s = Math.ceil(s0 / STEP) * STEP; s < s1; s += STEP) if (s > s0) out.push(at(s));
  out.push(at(s1));
  return out;
}

function ringNormalToward(ring, node, target) {
  const hit = nearestOnPoly(ring.poly, node.pos, true);
  const n = ring.poly.length;
  const t = norm(sub(ring.poly[(hit.i + 2) % n], ring.poly[(hit.i - 1 + n) % n]));
  let nrm = perp(t);
  if (dot(nrm, sub(target, node.pos)) < 0) nrm = mul(nrm, -1);
  return nrm;
}

export function buildNetwork(layout) {
  const rings = {};
  for (const r of layout.rings ?? []) rings[r.id] = { ...r, poly: resample(superellipse(r), STEP, true) };
  const nodes = {};
  for (const [id, def] of Object.entries(layout.nodes)) {
    if (Array.isArray(def)) nodes[id] = { id, pos: def };
    else {
      const ring = rings[def.ring];
      const hit = nearestOnPoly(ring.poly, def.near, true);
      nodes[id] = { id, pos: hit.p, ring: def.ring, ringS: hit.s };
    }
  }
  const edges = [];
  for (const ring of Object.values(rings)) {
    const on = Object.values(nodes).filter(n => n.ring === ring.id).sort((a, b) => a.ringS - b.ringS);
    const total = polyLength(ring.poly, true);
    on.forEach((a, k) => {
      const b = on[(k + 1) % on.length];
      const s1 = b.ringS + (k === on.length - 1 ? total : 0);
      const name = ring.arcNames?.[`${a.id}-${b.id}`] ?? ring.name;
      edges.push({ id: `${ring.id}-${a.id}-${b.id}`, name, cls: ring.class, district: ring.district ?? null,
        from: a.id, to: b.id, poly: resample(ringSlice(ring.poly, a.ringS, s1), STEP), ring: ring.id });
    });
  }
  for (const r of layout.roads) {
    const A = nodes[r.from], B = nodes[r.to];
    if (!A || !B) throw new Error(`Jalan ${r.id}: simpul ${!A ? r.from : r.to} tidak ada`);
    const via = r.via ?? [];
    let pts = [A.pos, ...via, B.pos];
    let dense;
    if (r.shape === 'poly') dense = roundedPolyline(pts, r.radius ?? layout.polyRadius ?? 10);
    else {
      // Ujung di lingkar: titik tuntun supaya jalan keluar tegak lurus terhadap lingkar.
      const lead = r.lead ?? 5;
      if (A.ring) pts.splice(1, 0, add(A.pos, mul(ringNormalToward(rings[A.ring], A, pts[1]), lead)));
      if (B.ring) pts.splice(pts.length - 1, 0, add(B.pos, mul(ringNormalToward(rings[B.ring], B, pts[pts.length - 2]), lead)));
      dense = catmullRom(pts);
    }
    edges.push({ id: r.id, name: r.name, cls: r.class ?? 'kampung', district: r.district ?? null,
      from: r.from, to: r.to, poly: resample(dense, STEP) });
  }
  for (const e of edges) e.length = polyLength(e.poly);
  return { nodes, edges, rings, halfWidth: (layout.roadWidth ?? 6) / 2 };
}

// Arah ujung ruas yang meninggalkan simpul, diukur pada jarak `at` sepanjang ruas.
function endDir(edge, atStart, at = 3) {
  const s = atStart ? at : edge.length - at;
  const { p } = pointAt(edge.poly, s);
  const base = atStart ? edge.poly[0] : edge.poly[edge.poly.length - 1];
  return norm(sub(p, base));
}

export function nodeEnds(net) {
  const ends = {};
  for (const id of Object.keys(net.nodes)) ends[id] = [];
  net.edges.forEach((e, i) => {
    ends[e.from].push({ edge: i, atStart: true, dir: endDir(e, true) });
    ends[e.to].push({ edge: i, atStart: false, dir: endDir(e, false) });
  });
  return ends;
}

export function validateNetwork(net, layout) {
  const checks = [];
  const add_ = (id, label, pass, value, limit) => checks.push({ id, label, pass, value, limit });
  const ends = nodeEnds(net);
  const ids = Object.keys(net.nodes);

  // Derajat.
  const deg = Object.fromEntries(ids.map(id => [id, ends[id].length]));
  const deadEnds = ids.filter(id => deg[id] === 1);
  const isolated = ids.filter(id => deg[id] === 0);
  add_('buntu', 'Simpul berderajat 1 (jalan buntu)', deadEnds.length === 0 && isolated.length === 0, deadEnds.length + isolated.length, 0);

  // Komponen terhubung.
  const parent = Object.fromEntries(ids.map(id => [id, id]));
  const find = x => parent[x] === x ? x : (parent[x] = find(parent[x]));
  for (const e of net.edges) parent[find(e.from)] = find(e.to);
  const comps = new Set(ids.filter(id => deg[id] > 0).map(find)).size;
  add_('komponen', 'Komponen jaringan', comps === 1, comps, 1);

  // Jembatan graf (Tarjan, sadar ruas ganda).
  const adj = Object.fromEntries(ids.map(id => [id, []]));
  net.edges.forEach((e, i) => { adj[e.from].push([e.to, i]); adj[e.to].push([e.from, i]); });
  const tin = {}, low = {}; let timer = 0; const bridges = [];
  const dfs = (v, pe) => {
    tin[v] = low[v] = timer++;
    for (const [u, ei] of adj[v]) {
      if (ei === pe) continue;
      if (tin[u] !== undefined) low[v] = Math.min(low[v], tin[u]);
      else { dfs(u, ei); low[v] = Math.min(low[v], low[u]); if (low[u] > tin[v]) bridges.push(net.edges[ei].id); }
    }
  };
  for (const id of ids) if (tin[id] === undefined && deg[id] > 0) dfs(id, -1);
  add_('jembatan', 'Ruas jembatan (kalau diputus, kota terbelah)', bridges.length === 0, bridges.length, 0);

  // Sudut persimpangan dan kelancaran sambungan derajat 2.
  const junctions = [];
  let minGap = 360, worst = null;
  const kinks = [];
  for (const id of ids) {
    const list = ends[id];
    if (list.length < 2) continue;
    const angs = list.map(x => Math.atan2(x.dir[1], x.dir[0]) * 180 / Math.PI).sort((a, b) => a - b);
    const gaps = angs.map((a, i) => ((i + 1 < angs.length ? angs[i + 1] : angs[0] + 360) - a));
    if (list.length === 2) { if (Math.min(...gaps) < 155) kinks.push(id); continue; }
    const g = Math.min(...gaps);
    junctions.push({ id, pos: net.nodes[id].pos, degree: list.length, minGap: Math.round(g) });
    if (g < minGap) { minGap = g; worst = id; }
  }
  add_('sudut', 'Sudut terkecil antar-jalan di persimpangan', minGap >= 60, `${Math.round(minGap)}° (${worst})`, '≥ 60°');
  add_('patah', 'Sambungan berderajat 2 yang menekuk', kinks.length === 0, kinks.length, 0);
  const maxDeg = Math.max(...junctions.map(j => j.degree), 0);
  add_('derajat', 'Derajat persimpangan terbesar', maxDeg <= 4, maxDeg, '≤ 4');

  // Jarak antarpersimpangan.
  let minSpace = Infinity, pair = '';
  for (let i = 0; i < junctions.length; i++) for (let j = i + 1; j < junctions.length; j++) {
    const d = dist(junctions[i].pos, junctions[j].pos);
    if (d < minSpace) { minSpace = d; pair = `${junctions[i].id}–${junctions[j].id}`; }
  }
  add_('jarak-simpang', 'Jarak terdekat antarpersimpangan', minSpace >= (layout.minJunctionSpacing ?? 18), `${minSpace.toFixed(1)} (${pair})`, `≥ ${layout.minJunctionSpacing ?? 18}`);

  // Jari-jari tikungan per kelas.
  const minR = layout.minRadius ?? { utama: 9, kampung: 7 };
  const radius = {};
  for (const e of net.edges) {
    const prof = curvatureProfile(e.poly, 8);
    let r = Infinity, at = null;
    prof.forEach((v, i) => { if (Math.abs(v) < r) { r = Math.abs(v); at = e.poly[i]; } });
    e.minRadius = r;
    const cls = e.cls;
    if (!radius[cls] || r < radius[cls].r) radius[cls] = { r, id: e.id, at };
  }
  for (const [cls, v] of Object.entries(radius))
    add_(`radius-${cls}`, `Tikungan tertajam jalan ${cls}`, v.r >= (minR[cls] ?? 7), (isFinite(v.r) ? `${v.r.toFixed(1)} (${v.id} di ${Math.round(v.at[0])},${Math.round(v.at[1])})` : 'semua lurus'), `≥ ${minR[cls] ?? 7}`);

  // Jalan tidak boleh bersilangan tanpa simpul atau terlalu berdekatan.
  const gapMin = layout.minRoadGap ?? 13;
  let closest = Infinity, closePair = '';
  const samples = net.edges.map(e => resample(e.poly, 1));
  for (let i = 0; i < net.edges.length; i++) for (let j = i + 1; j < net.edges.length; j++) {
    const a = net.edges[i], b = net.edges[j];
    const shared = [a.from, a.to].filter(n => n === b.from || n === b.to).map(n => net.nodes[n].pos);
    const excl = 16;
    const bs = samples[j].filter(p => shared.every(s => dist(p, s) > excl));
    for (const p of samples[i]) {
      if (shared.some(s => dist(p, s) <= excl)) continue;
      for (let k = 1; k < bs.length; k++) {
        const d = segDist(p, bs[k - 1], bs[k]);
        if (d < closest) { closest = d; closePair = `${a.id} / ${b.id}`; }
      }
    }
  }
  add_('celah-jalan', 'Jarak terdekat dua jalan di luar persimpangan', closest >= gapMin, `${closest.toFixed(1)} (${closePair})`, `≥ ${gapMin}`);
  return { checks, junctions, bridges, deadEnds, kinks };
}

// Rangkai ruas yang menyambung lurus (≥ 150°) di tiap simpul menjadi satu goresan.
// Ujung cabang yang berakhir di jalan tembus dipangkas setengah lebar jalan supaya sisi seberang tidak menggembung.
export function buildStrokes(net) {
  const ends = nodeEnds(net);
  const link = new Map();          // "edge:start|end" -> pasangan ujung
  const trimAt = new Map();
  const key = (edge, atStart) => `${edge}:${atStart ? 's' : 'e'}`;
  for (const [id, list] of Object.entries(ends)) {
    const pairs = [];
    for (let i = 0; i < list.length; i++) for (let j = i + 1; j < list.length; j++) {
      const d = dot(list[i].dir, list[j].dir);
      if (d <= Math.cos(150 * Math.PI / 180)) pairs.push([d, i, j]);
    }
    pairs.sort((a, b) => a[0] - b[0]);
    const used = new Set();
    for (const [, i, j] of pairs) {
      if (used.has(i) || used.has(j)) continue;
      used.add(i); used.add(j);
      link.set(key(list[i].edge, list[i].atStart), list[j]);
      link.set(key(list[j].edge, list[j].atStart), list[i]);
    }
    const through = used.size > 0;
    list.forEach((x, i) => { if (!used.has(i)) trimAt.set(key(x.edge, x.atStart), through ? net.halfWidth : 0); });
  }
  const seen = new Set();
  const strokes = [];
  net.edges.forEach((_, start) => {
    if (seen.has(start)) return;
    // Mundur ke ujung awal rangkaian.
    let e = start, atStart = true, guard = 0;
    while (link.has(key(e, atStart)) && guard++ < 999) {
      const nx = link.get(key(e, atStart));
      if (nx.edge === start) break;
      e = nx.edge; atStart = !nx.atStart;
    }
    // Maju dan kumpulkan.
    const chain = [];
    let cur = e, forward = atStart;   // forward=true: masuk dari ujung awal
    let closed = false;
    guard = 0;
    while (guard++ < 999) {
      chain.push({ edge: cur, forward });
      seen.add(cur);
      const exitKey = key(cur, !forward);
      if (!link.has(exitKey)) break;
      const nx = link.get(exitKey);
      if (seen.has(nx.edge)) { closed = nx.edge === chain[0].edge; break; }
      cur = nx.edge; forward = nx.atStart;
    }
    let poly = [];
    for (const { edge, forward: f } of chain) {
      const pts = f ? net.edges[edge].poly : [...net.edges[edge].poly].reverse();
      poly = poly.length ? poly.concat(pts.slice(1)) : pts.slice();
    }
    if (closed) poly.pop();
    else {
      const first = chain[0], last = chain[chain.length - 1];
      const t0 = trimAt.get(key(first.edge, first.forward)) ?? 0;
      const t1 = trimAt.get(key(last.edge, !last.forward)) ?? 0;
      poly = trimPoly(poly, t0, t1);
    }
    strokes.push({ edges: chain.map(c => net.edges[c.edge].id), closed, poly });
  });
  return strokes;
}

function trimPoly(poly, t0, t1) {
  const total = polyLength(poly);
  if (t0 + t1 >= total - 0.5) return poly;
  const out = [];
  let acc = 0;
  for (let i = 0; i < poly.length; i++) {
    if (i > 0) acc += dist(poly[i - 1], poly[i]);
    if (acc >= t0 && acc <= total - t1) out.push(poly[i]);
  }
  return out;
}
