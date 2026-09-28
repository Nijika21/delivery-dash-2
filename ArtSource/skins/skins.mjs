// Skin truk Delivery Dash 2. Semua skin mewarnai ulang bodi car.png (yang dipertahankan) lalu menempel motif.
// Koordinat motif = koordinat piksel car.png (486x1036). Bodi bak: x 42..430, y 317..1011.
// Slot tetap. Harga (revisi pengguna 26 Sep, bulat, maks 1200): 0/150/300/400/500/600/700/850/1000/1200.

const INK = '#26343A';
const CX = 236;

// Daerah dalam bak (di dalam bingkai warna tepi). Motif dipotong ke sini.
export const INNER = { x: 64, y: 340, w: 344, h: 648, r: 30 };

const rnd = seed => () => ((seed = (seed * 16807) % 2147483647) - 1) / 2147483646;
const scatter = (seed, n, fn, pad = 30) => {
  const r = rnd(seed); let s = '';
  for (let i = 0; i < n; i++) s += fn(INNER.x + pad + r() * (INNER.w - 2 * pad), INNER.y + pad + r() * (INNER.h - 2 * pad), r, i);
  return s;
};
const star4 = (x, y, s, c = '#fff') => `<path d="M${x} ${y - s}Q${x} ${y} ${x + s} ${y}Q${x} ${y} ${x} ${y + s}Q${x} ${y} ${x - s} ${y}Q${x} ${y} ${x} ${y - s}Z" fill="${c}"/>`;

export const SKINS = [
  { id: 'kurir', name: 'Kurir', price: 0, desc: 'Warna asli truk kurir.', fill: null },
  {
    id: 'kepik', name: 'Kepik', price: 150, desc: 'Merah dengan bintik hitam.',
    fill: '#E8483B', edge: '#B8322A', hideStripes: true,
    motif: () => `
      <rect x="${CX - 7}" y="340" width="14" height="648" fill="${INK}"/>
      ${[[140, 430, 34], [332, 430, 34], [120, 600, 28], [352, 600, 28], [150, 770, 38], [322, 770, 38], [128, 920, 26], [344, 920, 26], [236, 360, 0]]
        .map(([x, y, r]) => r ? `<circle cx="${x}" cy="${y}" r="${r}" fill="${INK}"/><circle cx="${x - r * 0.3}" cy="${y - r * 0.3}" r="${r * 0.22}" fill="#fff" opacity=".35"/>` : '').join('')}`,
  },
  {
    id: 'awan', name: 'Awan', price: 300, desc: 'Biru langit berhias awan.',
    fill: '#86CFF3', edge: '#4FAEE0', hideStripes: true,
    motif: () => {
      const cloud = (x, y, s) => `<g transform="translate(${x} ${y}) scale(${s})">
        <path d="M-70 20Q-92 20 -92 0Q-92 -22 -66 -22Q-60 -52 -28 -50Q-8 -76 24 -62Q52 -70 62 -40Q92 -40 92 -10Q92 20 66 20Z" fill="#D8EEF9" transform="translate(0 10)"/>
        <path d="M-70 20Q-92 20 -92 0Q-92 -22 -66 -22Q-60 -52 -28 -50Q-8 -76 24 -62Q52 -70 62 -40Q92 -40 92 -10Q92 20 66 20Z" fill="#FFFFFF"/></g>`;
      return cloud(160, 430, 1) + cloud(320, 590, 0.9) + cloud(150, 760, 1.1) + cloud(318, 920, 0.85) + star4(330, 420, 14) + star4(110, 880, 10);
    },
  },
  {
    id: 'pisang', name: 'Pisang', price: 400, desc: 'Kuning dengan bintik cokelat.',
    fill: '#FFD84A', edge: '#E4B21C', hideStripes: true,
    motif: () => `
      <path d="M64 340H408V372Q236 396 64 372Z" fill="#7FB65A"/>
      <path d="M64 988H408V962Q236 940 64 962Z" fill="#6B4A2A"/>
      ${scatter(7, 22, (x, y, r) => `<ellipse cx="${x}" cy="${y}" rx="${5 + r() * 9}" ry="${4 + r() * 6}" fill="#8A5A2B" opacity=".7" transform="rotate(${r() * 180} ${x} ${y})"/>`, 50)}
      <path d="M96 420Q84 660 108 930" fill="none" stroke="#FFF1A8" stroke-width="16" stroke-linecap="round"/>`,
  },
  {
    id: 'rimba', name: 'Rimba', price: 500, desc: 'Hijau dengan motif daun.',
    fill: '#5DB352', edge: '#3F8F3E', hideStripes: true,
    motif: () => {
      const leaf = (x, y, a, s, c) => `<g transform="translate(${x} ${y}) rotate(${a}) scale(${s})">
        <path d="M0 -60Q40 -20 0 60Q-40 -20 0 -60Z" fill="${c}"/><path d="M0 -48V52" stroke="#2B652B" stroke-width="5" stroke-linecap="round" opacity=".6"/>
        <path d="M0 -10L16 -26M0 12L18 -4M0 -10L-16 -26M0 12L-18 -4" stroke="#2B652B" stroke-width="4" stroke-linecap="round" opacity=".45"/></g>`;
      return [[130, 420, -30, 1.1, '#8BCB6E'], [320, 470, 35, 0.9, '#3F8F3E'], [210, 600, 10, 1.2, '#A5D98A'], [110, 740, 40, 0.9, '#3F8F3E'],
        [330, 720, -25, 1.1, '#8BCB6E'], [230, 880, -10, 1.0, '#3F8F3E'], [120, 930, 60, 0.8, '#A5D98A'], [350, 920, -50, 0.8, '#8BCB6E']]
        .map(p => leaf(...p)).join('');
    },
  },
  {
    id: 'zebra', name: 'Zebra', price: 600, desc: 'Belang hitam putih.',
    fill: '#F7F7F2', edge: '#D9D9D0', hideStripes: true,
    motif: () => {
      let s = '';
      for (let i = 0; i < 9; i++) {
        const y = 370 + i * 72, left = i % 2 === 0;
        s += left
          ? `<path d="M40 ${y - 16}Q140 ${y - 30} 230 ${y + 8}Q150 ${y + 4} 40 ${y + 22}Z" fill="${INK}"/>`
          : `<path d="M432 ${y - 16}Q332 ${y - 30} 242 ${y + 8}Q322 ${y + 4} 432 ${y + 22}Z" fill="${INK}"/>`;
      }
      return s;
    },
  },
  {
    id: 'stroberi', name: 'Stroberi', price: 700, desc: 'Merah muda, krim, dan meses warna-warni.',
    fill: '#FFB3C7', edge: '#EE86A5', hideStripes: true,
    motif: () => `
      <path d="M40 340H432V440Q410 480 388 440Q370 520 344 450Q320 490 300 440Q276 530 250 446Q228 492 206 440Q182 510 160 446Q140 484 118 440Q96 500 76 446Q60 470 40 440Z" fill="#FFF6E8"/>
      <path d="M86 380Q140 368 200 380" fill="none" stroke="#fff" stroke-width="12" stroke-linecap="round"/>
      ${scatter(11, 34, (x, y, r, i) => y < 520 ? '' : `<rect x="${x - 12}" y="${y - 4}" width="24" height="8" rx="4" fill="${['#F4C63F', '#1DB4E8', '#3F8F3E', '#FFFFFF', '#9C7BD6'][i % 5]}" transform="rotate(${r() * 180} ${x} ${y})"/>`, 40)}`,
  },
  {
    id: 'balap', name: 'Balap', price: 850, desc: 'Abu tua dengan garis balap jingga.',
    fill: '#3A4148', edge: '#23292E', hideStripes: true,
    motif: () => {
      // Dua garis balap dari depan ke belakang, lalu pita catur di buritan.
      const stripe = x => `<rect x="${x - 4}" y="330" width="44" height="620" fill="#fff"/><rect x="${x}" y="330" width="36" height="620" fill="#F59A2C"/><rect x="${x + 4}" y="330" width="9" height="620" fill="#FFC46B"/>`;
      let check = '';
      for (let r = 0; r < 2; r++) for (let c = 0; c < 12; c++) if ((r + c) % 2 === 0) check += `<rect x="${64 + c * 29}" y="${932 + r * 28}" width="29" height="28" fill="#fff"/>`;
      return stripe(CX - 54) + stripe(CX + 18)
        + `<rect x="40" y="928" width="400" height="64" fill="${INK}"/>${check}<rect x="40" y="924" width="400" height="8" fill="#F59A2C"/>`
        + `<path d="M96 400V880" stroke="#fff" stroke-width="12" stroke-linecap="round" opacity=".12"/>`;
    },
  },
  {
    id: 'pelangi', name: 'Pelangi', price: 1000, desc: 'Enam warna dari depan sampai belakang.',
    fill: '#FFFFFF', edge: '#E3E3E3', hideStripes: true,
    motif: () => ['#E8483B', '#F59A2C', '#F4C63F', '#5DB352', '#1DB4E8', '#8A63C9']
      .map((c, i) => `<rect x="40" y="${340 + i * 108}" width="400" height="${i === 5 ? 110 : 112}" fill="${c}"/>`).join('')
      + `<path d="M96 380V940" stroke="#fff" stroke-width="14" stroke-linecap="round" opacity=".35"/>`,
  },
  {
    id: 'galaksi', name: 'Galaksi', price: 1200, desc: 'Ungu malam penuh bintang.',
    fill: '#3B2A7A', edge: '#251760', hideStripes: true,
    motif: () => `
      <ellipse cx="170" cy="520" rx="130" ry="80" fill="#8A4FC9" opacity=".45" transform="rotate(-25 170 520)"/>
      <ellipse cx="320" cy="800" rx="120" ry="70" fill="#E0609E" opacity=".35" transform="rotate(30 320 800)"/>
      <ellipse cx="200" cy="900" rx="90" ry="50" fill="#1DB4E8" opacity=".3"/>
      ${scatter(5, 40, (x, y, r) => `<circle cx="${x}" cy="${y}" r="${1.5 + r() * 3.5}" fill="#fff" opacity="${0.5 + r() * 0.5}"/>`, 20)}
      ${star4(140, 420, 22)}${star4(350, 610, 16)}${star4(110, 860, 18)}${star4(330, 950, 12, '#FBE08A')}
      <circle cx="300" cy="440" r="42" fill="#F9C278"/><circle cx="288" cy="428" r="12" fill="#fff" opacity=".35"/>
      <ellipse cx="300" cy="440" rx="72" ry="16" fill="none" stroke="#FBE08A" stroke-width="8" transform="rotate(-20 300 440)"/>`,
  },
];

