// Geometri 2D untuk baker kota. Titik = [x, y] dalam unit dunia Unity.

export const add = (a, b) => [a[0] + b[0], a[1] + b[1]];
export const sub = (a, b) => [a[0] - b[0], a[1] - b[1]];
export const mul = (a, s) => [a[0] * s, a[1] * s];
export const dot = (a, b) => a[0] * b[0] + a[1] * b[1];
export const len = a => Math.hypot(a[0], a[1]);
export const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
export const norm = a => { const l = len(a) || 1; return [a[0] / l, a[1] / l]; };
export const perp = a => [-a[1], a[0]];
export const lerp = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
export const angleOf = a => Math.atan2(a[1], a[0]);
export const round3 = n => Math.round(n * 1000) / 1000;
export const pt3 = p => [round3(p[0]), round3(p[1])];

export function segDist(p, a, b) {
  const dx = b[0] - a[0], dy = b[1] - a[1];
  const l2 = dx * dx + dy * dy || 1e-9;
  let t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2;
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return Math.hypot(p[0] - a[0] - dx * t, p[1] - a[1] - dy * t);
}

export function polyLength(poly, closed = false) {
  let s = 0;
  for (let i = 1; i < poly.length; i++) s += dist(poly[i - 1], poly[i]);
  if (closed) s += dist(poly[poly.length - 1], poly[0]);
  return s;
}

// Titik berjarak sama sepanjang polyline (ujung tetap dipertahankan).
export function resample(poly, step, closed = false) {
  const pts = closed ? [...poly, poly[0]] : poly;
  const total = polyLength(pts);
  const n = Math.max(1, Math.round(total / step));
  const d = total / n;
  const out = [pts[0]];
  let seg = 1, acc = 0, prev = pts[0];
  for (let k = 1; k < n; k++) {
    let want = d * k - acc;
    while (seg < pts.length) {
      const l = dist(prev, pts[seg]);
      if (want <= l) { prev = lerp(prev, pts[seg], want / (l || 1)); acc = d * k; break; }
      want -= l; acc += l; prev = pts[seg]; seg++;
    }
    out.push(prev);
  }
  if (!closed) out.push(pts[pts.length - 1]);
  return out;
}

// Catmull-Rom sentripetal (alpha 0,5) melalui semua titik. Ujung memakai titik bayangan hasil pantulan.
export function catmullRom(points, samples = 40) {
  if (points.length < 2) return points.slice();
  const P = points.slice();
  const first = sub(mul(P[0], 2), P[1]);
  const last = sub(mul(P[P.length - 1], 2), P[P.length - 2]);
  const Q = [first, ...P, last];
  const out = [];
  const tj = (ti, a, b) => ti + Math.sqrt(Math.max(dist(a, b), 1e-6));
  for (let i = 0; i < Q.length - 3; i++) {
    const [p0, p1, p2, p3] = [Q[i], Q[i + 1], Q[i + 2], Q[i + 3]];
    const t0 = 0, t1 = tj(t0, p0, p1), t2 = tj(t1, p1, p2), t3 = tj(t2, p2, p3);
    for (let k = 0; k < samples; k++) {
      const t = t1 + (t2 - t1) * k / samples;
      const A1 = add(mul(p0, (t1 - t) / (t1 - t0)), mul(p1, (t - t0) / (t1 - t0)));
      const A2 = add(mul(p1, (t2 - t) / (t2 - t1)), mul(p2, (t - t1) / (t2 - t1)));
      const A3 = add(mul(p2, (t3 - t) / (t3 - t2)), mul(p3, (t - t2) / (t3 - t2)));
      const B1 = add(mul(A1, (t2 - t) / (t2 - t0)), mul(A2, (t - t0) / (t2 - t0)));
      const B2 = add(mul(A2, (t3 - t) / (t3 - t1)), mul(A3, (t - t1) / (t3 - t1)));
      out.push(add(mul(B1, (t2 - t) / (t2 - t1)), mul(B2, (t - t1) / (t2 - t1))));
    }
  }
  out.push(P[P.length - 1]);
  return out;
}

// Polyline lurus dengan sudut dibulatkan (untuk jalan grid Blok Paket). radius per titik tengah.
export function roundedPolyline(points, radius) {
  if (points.length < 3) return points.slice();
  const out = [points[0]];
  for (let i = 1; i < points.length - 1; i++) {
    const a = points[i - 1], b = points[i], c = points[i + 1];
    const u = norm(sub(a, b)), v = norm(sub(c, b));
    const theta = Math.acos(Math.max(-1, Math.min(1, dot(u, v))));
    if (theta > Math.PI - 1e-3) { out.push(b); continue; }
    const r = Array.isArray(radius) ? radius[i] : radius;
    const t = Math.min(r / Math.tan(theta / 2), dist(a, b) / 2, dist(b, c) / 2);
    const rr = t * Math.tan(theta / 2);
    const p1 = add(b, mul(u, t)), p2 = add(b, mul(v, t));
    const bis = norm(add(u, v));
    const center = add(b, mul(bis, rr / Math.sin(theta / 2)));
    let a1 = angleOf(sub(p1, center)), a2 = angleOf(sub(p2, center));
    let da = a2 - a1;
    while (da > Math.PI) da -= 2 * Math.PI;
    while (da < -Math.PI) da += 2 * Math.PI;
    const steps = Math.max(4, Math.ceil(Math.abs(da) * rr / 0.2));
    for (let k = 0; k <= steps; k++) {
      const ang = a1 + da * k / steps;
      out.push([center[0] + Math.cos(ang) * rr, center[1] + Math.sin(ang) * rr]);
    }
  }
  out.push(points[points.length - 1]);
  return out;
}

// Superelips tertutup: |x/a|^n + |y/b|^n = 1.
export function superellipse({ center, a, b, n }, count = 4000) {
  const out = [];
  for (let i = 0; i < count; i++) {
    const t = i / count * Math.PI * 2, c = Math.cos(t), s = Math.sin(t);
    out.push([center[0] + a * Math.sign(c) * Math.abs(c) ** (2 / n), center[1] + b * Math.sign(s) * Math.abs(s) ** (2 / n)]);
  }
  return out;
}

// Titik terdekat pada polyline: jarak, indeks segmen, parameter panjang busur.
export function nearestOnPoly(poly, p, closed = false) {
  let best = { d: Infinity, i: 0, s: 0, p: poly[0] };
  let acc = 0;
  const n = closed ? poly.length : poly.length - 1;
  for (let i = 0; i < n; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length];
    const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy || 1e-9, l = Math.sqrt(l2);
    let t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2;
    t = t < 0 ? 0 : t > 1 ? 1 : t;
    const q = [a[0] + dx * t, a[1] + dy * t], d = dist(p, q);
    if (d < best.d) best = { d, i, s: acc + l * t, p: q };
    acc += l;
  }
  return best;
}

// Titik dan arah pada panjang busur s.
export function pointAt(poly, s) {
  let acc = 0;
  for (let i = 1; i < poly.length; i++) {
    const l = dist(poly[i - 1], poly[i]);
    if (s <= acc + l || i === poly.length - 1) {
      const t = l ? Math.max(0, Math.min(1, (s - acc) / l)) : 0;
      return { p: lerp(poly[i - 1], poly[i], t), dir: norm(sub(poly[i], poly[i - 1])) };
    }
    acc += l;
  }
  return { p: poly[poly.length - 1], dir: [1, 0] };
}

// Jari-jari lengkung di tiap titik (titik berjarak sama, span = jarak ke tetangga dalam indeks).
// Tanda positif = pusat lengkung di kiri arah jalan.
export function curvatureProfile(poly, span) {
  const out = new Array(poly.length).fill(Infinity);
  for (let i = span; i < poly.length - span; i++) {
    const a = poly[i - span], b = poly[i], c = poly[i + span];
    const ab = dist(a, b), bc = dist(b, c), ca = dist(c, a);
    const cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
    const area2 = Math.abs(cross);
    out[i] = area2 < 1e-9 ? Infinity : (ab * bc * ca) / (2 * area2) * Math.sign(cross);
  }
  return out;
}

// Penghalusan Chaikin (untuk tepi danau).
export function chaikin(poly, passes = 3, closed = true) {
  let pts = poly;
  for (let k = 0; k < passes; k++) {
    const next = [];
    const n = closed ? pts.length : pts.length - 1;
    if (!closed) next.push(pts[0]);
    for (let i = 0; i < n; i++) {
      const a = pts[i], b = pts[(i + 1) % pts.length];
      next.push(lerp(a, b, 0.25), lerp(a, b, 0.75));
    }
    if (!closed) next.push(pts[pts.length - 1]);
    pts = next;
  }
  return pts;
}

export function pointInPoly(p, poly) {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[j], b = poly[i];
    if ((a[1] > p[1]) !== (b[1] > p[1]) && p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
  }
  return inside;
}

// Jarak bertanda ke poligon tertutup (negatif di dalam).
export function polySdf(p, poly) {
  let d = Infinity;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) d = Math.min(d, segDist(p, poly[j], poly[i]));
  return pointInPoly(p, poly) ? -d : d;
}

// Kotak bersudut bulat, radius per kuadran: [kananAtas, kananBawah, kiriBawah, kiriAtas].
export function roundBoxSdf(p, center, half, radii) {
  const q0 = p[0] - center[0], q1 = p[1] - center[1];
  const r = q0 > 0 ? (q1 > 0 ? radii[0] : radii[1]) : (q1 > 0 ? radii[3] : radii[2]);
  const dx = Math.abs(q0) - half[0] + r, dy = Math.abs(q1) - half[1] + r;
  return Math.min(Math.max(dx, dy), 0) + Math.hypot(Math.max(dx, 0), Math.max(dy, 0)) - r;
}

// Gabungan membulat (fillet lingkaran berjari-jari r) tanpa menggembung saat |a-b| >= r.
export const unionRound = (a, b, r) => {
  const ua = r - a > 0 ? r - a : 0, ub = r - b > 0 ? r - b : 0;
  return Math.max(r, Math.min(a, b)) - Math.hypot(ua, ub);
};
