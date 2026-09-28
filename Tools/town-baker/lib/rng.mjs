// Acak berbenih (mulberry32) dan noise nilai halus. Hasil bake harus sama persis di setiap mesin.

export function rng(seed) {
  let a = seed >>> 0;
  const next = () => {
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  next.range = (lo, hi) => lo + (hi - lo) * next();
  next.int = n => Math.floor(next() * n);
  next.pick = arr => arr[Math.floor(next() * arr.length)];
  next.weighted = weights => {
    const entries = Object.entries(weights);
    let total = 0; for (const [, w] of entries) total += w;
    let r = next() * total;
    for (const [k, w] of entries) { r -= w; if (r <= 0) return k; }
    return entries[entries.length - 1][0];
  };
  next.shuffle = arr => { for (let i = arr.length - 1; i > 0; i--) { const j = Math.floor(next() * (i + 1)); [arr[i], arr[j]] = [arr[j], arr[i]]; } return arr; };
  return next;
}

const hash = (x, y, seed) => {
  let h = Math.imul(x, 374761393) ^ Math.imul(y, 668265263) ^ Math.imul(seed, 2246822519);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
};

// Noise nilai 0..1 dengan interpolasi smoothstep, beberapa oktaf.
export function noise2(seed, scale = 16, octaves = 2) {
  return (x, y) => {
    let v = 0, amp = 1, total = 0, s = scale;
    for (let o = 0; o < octaves; o++) {
      const gx = x / s, gy = y / s, ix = Math.floor(gx), iy = Math.floor(gy);
      const fx = gx - ix, fy = gy - iy, ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
      const a = hash(ix, iy, seed + o), b = hash(ix + 1, iy, seed + o), c = hash(ix, iy + 1, seed + o), d = hash(ix + 1, iy + 1, seed + o);
      v += amp * (a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy);
      total += amp; amp *= 0.5; s *= 0.5;
    }
    return v / total;
  };
}
