// Rumah Delivery Dash 2 — template SVG bertoken warna.
// Sudut pandang: tampak depan-atas seperti set rumah lama (fasad di bawah, atap "memanjang ke belakang" ke atas).
// Sisi bawah gambar = sisi depan (pintu); di game sprite diputar supaya sisi ini menghadap jalan.
// Semua rumah digambar di kanvas 256x256; garis tinta sama dengan logo dan komponen UI.

export const INK = '#26343A';
const GLASS = '#BFE9F8', GLASS_DEEP = '#8FD0EA', FRAME = '#FFFDF6';
const WOOD = '#9A6A45', WOOD_DARK = '#6F4A2F', WOOD_LIGHT = '#C0925F', STONE = '#CFC6AA', LEAF = '#5E9E4B', LEAF_DARK = '#3F7A36';

export const COLORWAYS = {
  bata:  { label: 'Bata',  R: '#C9603F', Rd: '#9A442B', Rl: '#E3835F', W: '#F4E4C4', Wd: '#DCC49B', D: '#7C4B2E', Dd: '#5F3822', A: '#F4C63F' },
  laut:  { label: 'Laut',  R: '#2384B8', Rd: '#16628A', Rl: '#58ADDA', W: '#F3F6F2', Wd: '#D2DEDC', D: '#F09A2C', Dd: '#C46A12', A: '#FF8FA3' },
  daun:  { label: 'Daun',  R: '#4F9B47', Rd: '#367335', Rl: '#79BC6E', W: '#F8DB8E', Wd: '#E0BD64', D: '#B9583A', Dd: '#8A3D27', A: '#FFFFFF' },
  senja: { label: 'Senja', R: '#7D4F93', Rd: '#5B3671', Rl: '#A078B6', W: '#D2ECDF', Wd: '#A9D3BF', D: '#F4C63F', Dd: '#B98C12', A: '#F9C278' },
};

// ---------- potongan kecil ----------
const win = (x, y, w, h) => `
  <rect x="${x}" y="${y}" width="${w}" height="${h}" rx="6" fill="${FRAME}" stroke="${INK}" stroke-width="4"/>
  <rect x="${x + 5}" y="${y + 5}" width="${w - 10}" height="${h - 10}" rx="3" fill="${GLASS}"/>
  <rect x="${x + 5}" y="${y + 5 + (h - 10) * 0.55}" width="${w - 10}" height="${(h - 10) * 0.45}" rx="3" fill="${GLASS_DEEP}"/>
  <path d="M${x + w / 2} ${y + 5}V${y + h - 5}M${x + 5} ${y + h / 2}H${x + w - 5}" stroke="${FRAME}" stroke-width="4"/>
  <rect x="${x + 8}" y="${y + 8}" width="${(w - 10) * 0.2}" height="${(h - 10) * 0.28}" rx="2" fill="#fff" opacity=".85"/>`;

const roundWin = (cx, cy, r) => `
  <circle cx="${cx}" cy="${cy}" r="${r}" fill="${FRAME}" stroke="${INK}" stroke-width="4"/>
  <circle cx="${cx}" cy="${cy}" r="${r - 5}" fill="${GLASS}"/>
  <path d="M${cx - r + 5} ${cy}H${cx + r - 5}M${cx} ${cy - r + 5}V${cy + r - 5}" stroke="${FRAME}" stroke-width="3"/>`;

const door = (x, y, w, h, c) => `
  <path d="M${x} ${y + h}V${y + w / 2}A${w / 2} ${w / 2} 0 0 1 ${x + w} ${y + w / 2}V${y + h}Z" fill="${c.D}" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>
  <path d="M${x + 7} ${y + h - 6}V${y + w / 2 + 2}A${w / 2 - 7} ${w / 2 - 7} 0 0 1 ${x + w - 7} ${y + w / 2 + 2}V${y + h - 6}" fill="none" stroke="${c.Dd}" stroke-width="3"/>
  <circle cx="${x + w - 9}" cy="${y + h * 0.62}" r="3" fill="${c.A === '#FFFFFF' ? '#F4C63F' : c.A}" stroke="${INK}" stroke-width="1.5"/>`;

const step = (x, y, w) => `<rect x="${x}" y="${y}" width="${w}" height="8" rx="3" fill="${STONE}" stroke="${INK}" stroke-width="4"/>`;

const flowerBox = (x, y, w, c) => {
  let dots = '';
  for (let i = 0; i < 3; i++) dots += `<circle cx="${x + w * (0.2 + i * 0.3)}" cy="${y - 2}" r="5" fill="${c.A === '#FFFFFF' ? '#FF8FA3' : c.A}" stroke="${INK}" stroke-width="2"/>`;
  return `<rect x="${x}" y="${y}" width="${w}" height="10" rx="3" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
    <path d="M${x + 4} ${y}q${w / 4} -8 ${w / 2 - 4} 0q${w / 4} -8 ${w / 2 - 4} 0" fill="${LEAF}" stroke="${INK}" stroke-width="2"/>${dots}`;
};

// Pita atap pelana: tinta tebal lalu warna atap lalu kilap.
const gableBand = (x1, y1, ax, ay, x2, y2, c, width = 28) => `
  <path d="M${x1} ${y1}L${ax} ${ay}L${x2} ${y2}" fill="none" stroke="${INK}" stroke-width="${width + 12}" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M${x1} ${y1}L${ax} ${ay}L${x2} ${y2}" fill="none" stroke="${c.R}" stroke-width="${width}" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M${x1 + 10} ${y1 - 12}L${ax} ${ay - 4}L${x2 - 10} ${y2 - 12}" fill="none" stroke="${c.Rl}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>`;

// Lempeng atap belakang (kedalaman yang terlihat dari atas).
const backSlab = (x1, y1, ax, ay, x2, y2, depth, c) => `
  <path d="M${x1} ${y1}L${ax} ${ay}L${x2} ${y2}L${x2} ${y2 - depth}L${ax} ${ay - depth}L${x1} ${y1 - depth}Z" fill="${c.Rd}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
  <path d="M${x1 + 8} ${y1 - depth + 10}L${ax} ${ay - depth + 12}L${x2 - 8} ${y2 - depth + 10}" fill="none" stroke="${c.R}" stroke-width="3" stroke-linejoin="round" opacity=".7"/>`;

const chimney = (x, y, h) => `
  <rect x="${x}" y="${y}" width="18" height="${h}" fill="#C98B62" stroke="${INK}" stroke-width="5"/>
  <rect x="${x - 4}" y="${y - 6}" width="26" height="9" rx="2" fill="#3A4A50" stroke="${INK}" stroke-width="3"/>`;

// ---------- tipe rumah ----------
const TYPES = {
  pelana: {
    label: 'Pelana',
    note: 'Atap segitiga menghadap depan, jendela bulat di loteng.',
    draw: c => `
      ${backSlab(40, 140, 128, 52, 216, 140, 34, c)}
      ${chimney(166, 36, 36)}
      <path d="M52 236V140L128 70L204 140V236Z" fill="${c.W}"/>
      <path d="M58 146L128 82L198 146" fill="none" stroke="${c.Wd}" stroke-width="12"/>
      <rect x="55" y="220" width="146" height="14" fill="${c.Wd}"/>
      ${roundWin(128, 114, 14)}
      ${win(68, 160, 38, 36)}${win(150, 160, 38, 36)}
      ${flowerBox(66, 202, 42, c)}${flowerBox(148, 202, 42, c)}
      ${door(110, 184, 36, 52, c)}
      <path d="M52 236V140L128 70L204 140V236Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      ${gableBand(34, 148, 128, 58, 222, 148, c)}
      ${step(100, 232, 56)}`,
  },
  perisai: {
    label: 'Perisai',
    note: 'Atap limas lebar, rumah rendah dan panjang.',
    draw: c => `
      <rect x="40" y="146" width="176" height="90" fill="${c.W}"/>
      <rect x="43" y="146" width="170" height="12" fill="${c.Wd}"/>
      <rect x="43" y="220" width="170" height="14" fill="${c.Wd}"/>
      ${win(56, 166, 42, 36)}${win(158, 166, 42, 36)}
      ${flowerBox(54, 208, 46, c)}${flowerBox(156, 208, 46, c)}
      ${door(112, 180, 32, 56, c)}
      <circle cx="152" cy="176" r="5" fill="#FFE9A3" stroke="${INK}" stroke-width="2.5"/>
      <rect x="40" y="146" width="176" height="90" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <path d="M22 142L72 56H184L234 142Z" fill="${c.R}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <path d="M26 139L72 60L86 139Z" fill="${c.Rd}"/><path d="M230 139L184 60L170 139Z" fill="${c.Rd}"/>
      <path d="M58 84H198M44 112H212" stroke="${c.Rd}" stroke-width="3" opacity=".75"/>
      <path d="M74 60H182" stroke="${c.Rl}" stroke-width="5" stroke-linecap="round"/>
      <path d="M22 142L72 56H184L234 142Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <rect x="18" y="136" width="220" height="14" rx="6" fill="${c.Rd}" stroke="${INK}" stroke-width="5"/>
      ${step(104, 232, 48)}`,
  },
  panggung: {
    label: 'Panggung',
    note: 'Rumah kayu di atas tiang, tangga turun ke depan.',
    draw: c => `
      ${[58, 90, 154, 186].map(x => `<rect x="${x}" y="188" width="12" height="50" fill="${WOOD}" stroke="${INK}" stroke-width="4"/>`).join('')}
      <rect x="56" y="214" width="144" height="6" fill="${WOOD_DARK}" stroke="${INK}" stroke-width="2"/>
      ${backSlab(28, 126, 128, 36, 228, 126, 28, c)}
      <rect x="48" y="112" width="160" height="74" fill="${c.W}"/>
      <path d="M52 118L128 52L204 118Z" fill="${c.Wd}"/>
      <path d="M116 96h24M112 104h32" stroke="${INK}" stroke-width="3" stroke-linecap="round" opacity=".55"/>
      ${[64, 80, 96, 160, 176, 192].map(x => `<path d="M${x} 122V184" stroke="${c.Wd}" stroke-width="3"/>`).join('')}
      <rect x="50" y="130" width="10" height="36" rx="2" fill="${c.R}" stroke="${INK}" stroke-width="3"/>
      ${win(62, 132, 34, 32)}
      <rect x="98" y="130" width="10" height="36" rx="2" fill="${c.R}" stroke="${INK}" stroke-width="3"/>
      ${win(160, 132, 34, 32)}
      <rect x="114" y="126" width="28" height="60" rx="3" fill="${c.D}" stroke="${INK}" stroke-width="4"/>
      <circle cx="136" cy="158" r="2.6" fill="${INK}"/>
      <path d="M48 112H208V186H48Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <rect x="40" y="182" width="176" height="12" rx="3" fill="#8A5A3B" stroke="${INK}" stroke-width="5"/>
      ${[0, 1, 2, 3].map(i => `<rect x="${112 - i * 3}" y="${196 + i * 11}" width="${32 + i * 6}" height="7" rx="2" fill="${WOOD_LIGHT}" stroke="${INK}" stroke-width="3"/>`).join('')}
      ${gableBand(26, 124, 128, 44, 230, 124, c, 22)}`,
  },
  warung: {
    label: 'Warung',
    note: 'Toko kecil dengan papan nama, tenda garis, dan etalase.',
    draw: c => {
      let awning = '';
      for (let i = 0; i < 7; i++) {
        const x = 30 + i * 28, col = i % 2 ? '#FFFDF6' : c.R;
        awning += `<path d="M${x} 122h28v26a14 14 0 0 1 -28 0z" fill="${col}" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>`;
      }
      return `
      <rect x="36" y="118" width="184" height="118" fill="${c.W}"/>
      <rect x="39" y="150" width="178" height="16" fill="${c.Wd}"/>
      <rect x="39" y="222" width="178" height="12" fill="${c.Wd}"/>
      <rect x="50" y="172" width="96" height="50" rx="5" fill="${FRAME}" stroke="${INK}" stroke-width="4"/>
      <rect x="55" y="177" width="86" height="40" rx="3" fill="${GLASS}"/>
      <path d="M58 204H138" stroke="${WOOD}" stroke-width="4"/>
      <circle cx="72" cy="196" r="7" fill="#F09A2C" stroke="${INK}" stroke-width="2.5"/>
      <circle cx="90" cy="197" r="6" fill="#E84A4A" stroke="${INK}" stroke-width="2.5"/>
      <circle cx="106" cy="196" r="7" fill="#F4C63F" stroke="${INK}" stroke-width="2.5"/>
      <circle cx="124" cy="197" r="6" fill="#7FB65A" stroke="${INK}" stroke-width="2.5"/>
      <rect x="58" y="181" width="12" height="9" rx="2" fill="#fff" opacity=".85"/>
      <rect x="44" y="218" width="108" height="10" rx="3" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
      <rect x="162" y="170" width="44" height="66" rx="4" fill="${c.D}" stroke="${INK}" stroke-width="4"/>
      <rect x="169" y="177" width="30" height="28" rx="3" fill="${GLASS}" stroke="${INK}" stroke-width="3"/>
      <circle cx="198" cy="214" r="3" fill="#FFFDF6" stroke="${INK}" stroke-width="1.5"/>
      <rect x="36" y="118" width="184" height="118" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <rect x="28" y="62" width="200" height="62" rx="10" fill="${c.R}" stroke="${INK}" stroke-width="6"/>
      <path d="M36 72H220" stroke="${c.Rl}" stroke-width="4" stroke-linecap="round"/>
      <rect x="62" y="76" width="132" height="38" rx="8" fill="#FFFDF6" stroke="${INK}" stroke-width="4"/>
      <path d="M110 88h30v13a9 9 0 0 1 -9 9h-12a9 9 0 0 1 -9 -9z" fill="${c.D}" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>
      <path d="M140 92a6 6 0 0 1 0 12" fill="none" stroke="${INK}" stroke-width="3"/>
      <path d="M118 84q-4 -5 0 -9M126 84q-4 -5 0 -9M134 84q-4 -5 0 -9" fill="none" stroke="${c.Rd}" stroke-width="2.5" stroke-linecap="round"/>
      <circle cx="84" cy="95" r="5" fill="${c.A === '#FFFFFF' ? '#F4C63F' : c.A}" stroke="${INK}" stroke-width="2"/>
      <circle cx="172" cy="95" r="5" fill="${c.A === '#FFFFFF' ? '#F4C63F' : c.A}" stroke="${INK}" stroke-width="2"/>
      ${awning}
      ${step(160, 232, 48)}`;
    },
  },
  duaLantai: {
    label: 'Dua lantai',
    note: 'Rumah tinggi dengan balkon kecil.',
    draw: c => `
      ${backSlab(46, 100, 128, 26, 210, 100, 24, c)}
      ${chimney(74, 30, 34)}
      <path d="M62 236V100L128 42L194 100V236Z" fill="${c.W}"/>
      <path d="M68 104L128 54L188 104" fill="none" stroke="${c.Wd}" stroke-width="10"/>
      <rect x="65" y="160" width="126" height="8" fill="${c.Wd}"/>
      <rect x="65" y="222" width="126" height="12" fill="${c.Wd}"/>
      ${roundWin(128, 82, 11)}
      ${win(78, 106, 36, 34)}${win(142, 106, 36, 34)}
      <rect x="70" y="138" width="116" height="5" rx="2" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
      ${[76, 88, 100, 112, 124, 136, 148, 160, 172].map(x => `<path d="M${x + 4} 143V154" stroke="${INK}" stroke-width="3"/>`).join('')}
      <rect x="68" y="152" width="120" height="7" rx="2" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
      ${win(78, 176, 36, 34)}
      ${flowerBox(76, 214, 40, c)}
      ${door(142, 178, 36, 58, c)}
      <path d="M62 236V100L128 42L194 100V236Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      ${gableBand(46, 106, 128, 34, 210, 106, c, 26)}
      ${step(136, 232, 48)}`,
  },
  beranda: {
    label: 'Beranda kebun',
    note: 'Rumah dengan teras samping, pot gantung, dan bedeng bunga.',
    draw: c => `
      <rect x="150" y="224" width="84" height="12" rx="3" fill="${WOOD}" stroke="${INK}" stroke-width="4"/>
      <rect x="166" y="162" width="10" height="64" fill="${WOOD_LIGHT}" stroke="${INK}" stroke-width="4"/>
      <rect x="216" y="166" width="10" height="60" fill="${WOOD_LIGHT}" stroke="${INK}" stroke-width="4"/>
      <rect x="182" y="204" width="30" height="7" rx="2" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
      <path d="M186 211v10M208 211v10" stroke="${INK}" stroke-width="3"/>
      ${backSlab(26, 132, 96, 60, 166, 132, 24, c)}
      <path d="M36 236V134L96 78L156 134V236Z" fill="${c.W}"/>
      <path d="M42 138L96 90L150 138" fill="none" stroke="${c.Wd}" stroke-width="10"/>
      <rect x="39" y="222" width="114" height="12" fill="${c.Wd}"/>
      ${roundWin(96, 114, 12)}
      ${win(48, 160, 40, 36)}
      ${door(106, 178, 36, 58, c)}
      <path d="M36 236V134L96 78L156 134V236Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      ${gableBand(24, 140, 96, 68, 168, 140, c, 24)}
      <path d="M146 150L238 160V176L146 166Z" fill="${c.R}" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
      <path d="M150 158L234 167" stroke="${c.Rl}" stroke-width="3" stroke-linecap="round"/>
      <path d="M196 170V182" stroke="${INK}" stroke-width="2.5"/>
      <path d="M189 182h14l-2 10h-10z" fill="${c.D}" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>
      <circle cx="191" cy="181" r="6" fill="${LEAF}" stroke="${INK}" stroke-width="2"/><circle cx="201" cy="180" r="6" fill="${LEAF}" stroke="${INK}" stroke-width="2"/>
      <path d="M188 186q-4 8 -2 14M204 186q4 8 2 14" fill="none" stroke="${LEAF_DARK}" stroke-width="3" stroke-linecap="round"/>
      ${[20, 36, 52, 68].map((x, i) => `<circle cx="${x + 14}" cy="238" r="9" fill="${LEAF}" stroke="${INK}" stroke-width="3"/><circle cx="${x + 14}" cy="233" r="4" fill="${['#FF8FA3', '#F4C63F', '#FFFFFF', '#F09A2C'][i]}" stroke="${INK}" stroke-width="1.5"/>`).join('')}
      ${step(100, 232, 48)}`,
  },
};

// Rumah tutorial: satu-satunya dengan menara bundar, bendera, dan bintang.
const TUTORIAL = {
  label: 'Rumah pertama (tutorial)',
  note: 'Tujuan antar pertama. Siluet menara dan bendera membuatnya mudah dikenali dari jauh.',
  draw: () => {
    const c = { ...COLORWAYS.laut, D: '#F09A2C', Dd: '#C46A12', A: '#F4C63F' };
    const star = (cx, cy, r) => {
      let d = '';
      for (let i = 0; i < 10; i++) {
        const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? r * 0.48 : r;
        d += `${i ? 'L' : 'M'}${(cx + rr * Math.cos(a)).toFixed(1)} ${(cy + rr * Math.sin(a)).toFixed(1)}`;
      }
      return `<path d="${d}Z" fill="#F4C63F" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>`;
    };
    return `
      ${backSlab(80, 150, 152, 80, 224, 150, 28, c)}
      <path d="M94 236V150L152 96L210 150V236Z" fill="${c.W}"/>
      <path d="M100 154L152 108L204 154" fill="none" stroke="${c.Wd}" stroke-width="10"/>
      <rect x="97" y="222" width="110" height="12" fill="${c.Wd}"/>
      ${star(152, 134, 14)}
      ${win(104, 166, 34, 32)}${win(168, 166, 34, 32)}
      ${door(136, 186, 32, 50, c)}
      <path d="M94 236V150L152 96L210 150V236Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      ${gableBand(78, 158, 152, 88, 226, 158, c, 24)}
      <rect x="30" y="112" width="64" height="124" rx="10" fill="${c.W}" stroke="${INK}" stroke-width="6"/>
      <rect x="74" y="116" width="16" height="116" rx="6" fill="${c.Wd}"/>
      <path d="M48 176V160a13 13 0 0 1 26 0V176Z" fill="${FRAME}" stroke="${INK}" stroke-width="4"/>
      <path d="M53 174V161a8 8 0 0 1 16 0V174Z" fill="${GLASS}"/>
      <rect x="44" y="200" width="36" height="8" rx="3" fill="${c.R}" stroke="${INK}" stroke-width="3"/>
      <path d="M22 120L62 34L102 120Z" fill="${c.R}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <path d="M62 38L99 118H62Z" fill="${c.Rd}"/>
      <path d="M22 120L62 34L102 120Z" fill="none" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <rect x="18" y="114" width="88" height="12" rx="6" fill="${c.Rd}" stroke="${INK}" stroke-width="4"/>
      <path d="M62 36V10" stroke="${INK}" stroke-width="4" stroke-linecap="round"/>
      <path d="M63 10L90 18L63 26Z" fill="#F4C63F" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>
      <rect x="219" y="204" width="6" height="34" fill="${WOOD}" stroke="${INK}" stroke-width="3"/>
      <rect x="207" y="188" width="30" height="20" rx="8" fill="#C9603F" stroke="${INK}" stroke-width="4"/>
      <path d="M233 192V178L243 182L233 186" fill="#F4C63F" stroke="${INK}" stroke-width="2.5" stroke-linejoin="round"/>
      ${step(130, 232, 44)}`;
  },
};

export const HOUSE_TYPES = Object.keys(TYPES);

export function houseSvg(type, colorway, size = 256) {
  const body = type === 'tutorial' ? TUTORIAL.draw() : TYPES[type].draw(COLORWAYS[colorway]);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256">${body}</svg>`;
}

export const houseInfo = type => (type === 'tutorial' ? TUTORIAL : TYPES[type]);
