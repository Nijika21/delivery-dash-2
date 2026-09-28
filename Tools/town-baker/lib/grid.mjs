// Perluasan `grid` di layout: kolom dan baris jalan lurus jadi simpul + ruas biasa.
// Simpul pojok luar dibuang; dua ruas keliling yang bertemu di sana digabung jadi satu ruas bertikungan bulat.
// Format:
//   "grid": { "xs": [...], "ys": [...], "cornerRadius": 12,
//             "columns": [nama per kolom], "rows": [nama per baris],
//             "utama": { "columns": [indeks], "rows": [indeks] },
//             "omit": ["h2_3", "v1_4"],             // h{i}_{j}: ruas mendatar dari kolom i ke i+1 di baris j; v{i}_{j}: ruas tegak dari baris j ke j+1 di kolom i
//             "shift": { "g3_2": [4, 0] },          // geser simpul supaya jalan tidak lurus sempurna (boulevard melengkung)
//             "corners": { "6_5": 30 } }            // radius pojok per sudut (default cornerRadius)
export function expandGrid(layout) {
  const g = layout.grid;
  if (!g) return layout;
  const { xs, ys } = g;
  const nx = xs.length, ny = ys.length;
  const nodes = { ...(layout.nodes ?? {}) };
  const roads = [...(layout.roads ?? [])];
  const omit = new Set(g.omit ?? []);
  const id = (i, j) => `g${i}_${j}`;
  const corner = (i, j) => (i === 0 || i === nx - 1) && (j === 0 || j === ny - 1);
  const cls = (kind, k) => ((g.utama?.[kind] ?? []).includes(k) ? 'utama' : 'kampung');
  const at = (i, j) => { const d = g.shift?.[id(i, j)] ?? [0, 0]; return [xs[i] + d[0], ys[j] + d[1]]; };
  for (let i = 0; i < nx; i++) for (let j = 0; j < ny; j++) if (!corner(i, j)) nodes[id(i, j)] = at(i, j);
  for (let j = 0; j < ny; j++) for (let i = 0; i + 1 < nx; i++) {
    const rid = `h${i}_${j}`;
    if (omit.has(rid) || corner(i, j) || corner(i + 1, j)) continue;
    roads.push({ id: rid, name: g.rows?.[j] ?? `Jalan Baris ${j + 1}`, class: cls('rows', j), from: id(i, j), to: id(i + 1, j), shape: 'poly' });
  }
  for (let i = 0; i < nx; i++) for (let j = 0; j + 1 < ny; j++) {
    const rid = `v${i}_${j}`;
    if (omit.has(rid) || corner(i, j) || corner(i, j + 1)) continue;
    roads.push({ id: rid, name: g.columns?.[i] ?? `Jalan Kolom ${i + 1}`, class: cls('columns', i), from: id(i, j), to: id(i, j + 1), shape: 'poly' });
  }
  // Pojok: satu ruas dari tetangga mendatar, lewat pojok (dibulatkan), ke tetangga tegak.
  for (const [i, j] of [[0, 0], [nx - 1, 0], [nx - 1, ny - 1], [0, ny - 1]]) {
    const hi = i === 0 ? 1 : nx - 2, vj = j === 0 ? 1 : ny - 2;
    roads.push({ id: `pojok${i}_${j}`, name: g.rows?.[j] ?? `Jalan Baris ${j + 1}`, class: cls('rows', j),
      from: id(hi, j), to: id(i, vj), via: [at(i, j)], shape: 'poly', radius: g.corners?.[`${i}_${j}`] ?? g.cornerRadius ?? 12 });
  }
  return { ...layout, nodes, roads };
}
