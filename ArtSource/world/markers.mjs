// Penanda dalam game: zona berhenti, area selesai kerja, panah petunjuk, dan pickup.
// Zona dan area adalah cat di tanah (tanpa garis tinta tebal) supaya tidak terlihat seperti benda yang menghalangi.
// Pickup dan panah adalah benda, jadi memakai garis tinta seperti rumah.

import { INK } from '../houses/houses.mjs';

const f = n => +n.toFixed(2);

// ---------- ikon kecil (pusat 0,0, kira-kira ±30) ----------
export const ICONS = {
  // Kotak paket isometrik datar, meniru warna package.png.
  paket: () => `
    <path d="M0 -30L28 -16V16L0 30L-28 16V-16Z" fill="#FBAE42"/>
    <path d="M0 -2L-28 -16V16L0 30Z" fill="#E27B1C"/>
    <path d="M0 -30L28 -16L0 -2L-28 -16Z" fill="#FCCB8E"/>
    <path d="M14 -23L-14 -9V-1L-8 2V-6L20 -20Z" fill="#DDF0FA"/>`,
  rumah: () => `
    <path d="M-24 -2L0 -26L24 -2V26H-24Z" fill="#FFFDF6" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
    <path d="M-30 0L0 -30L30 0" fill="none" stroke="#C9603F" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M-8 26V10a8 8 0 0 1 16 0V26" fill="#F09A2C" stroke="${INK}" stroke-width="4"/>`,
  // Tangan terbuka melambai. Semua bagian digambar dua lapis (garis tinta dulu, isi di atasnya)
  // supaya siluetnya menyatu tanpa garis bertumpuk; celah antarjari tampil sebagai garis tinta tipis.
  tangan: () => {
    const parts = [
      '<rect x="-15" y="-4" width="33" height="28" rx="11"/>',                                   // telapak
      '<rect x="-15" y="-22" width="7.6" height="26" rx="3.8"/>',                                // telunjuk
      '<rect x="-6.4" y="-27" width="7.6" height="31" rx="3.8"/>',                               // tengah
      '<rect x="2.2" y="-24" width="7.6" height="28" rx="3.8"/>',                                // manis
      '<rect x="10.8" y="-17" width="7.2" height="22" rx="3.6"/>',                               // kelingking
      '<rect x="-4.2" y="-19" width="8.4" height="22" rx="4.2" transform="translate(-14 12) rotate(-42)"/>', // jempol
    ];
    const layer = attrs => parts.map(p => p.replace('/>', ` ${attrs}/>`)).join('');
    return `<g transform="translate(-2 0)">
      ${layer(`fill="${INK}" stroke="${INK}" stroke-width="9" stroke-linejoin="round"`)}
      ${layer('fill="#F9C278"')}
      <path d="M-8 20Q2 25 14 18" fill="none" stroke="#E0A35A" stroke-width="3.5" stroke-linecap="round"/>
      <path d="M-12.5 -18V-8" stroke="#fff" stroke-width="2.5" stroke-linecap="round" opacity=".6"/>
      <rect x="-12" y="23" width="27" height="9" rx="3" fill="#1DB4E8" stroke="${INK}" stroke-width="4"/>
      <path d="M25 -20Q31 -14 29 -6M30 -27Q39 -17 35 -4" fill="none" stroke="${INK}" stroke-width="3.5" stroke-linecap="round"/>
    </g>`;
  },
  // Amplop dengan segel merah tepat di ujung runcing tutupnya.
  surat: () => `
    <rect x="-28" y="-19" width="56" height="38" rx="6" fill="#FFFDF6" stroke="${INK}" stroke-width="5"/>
    <path d="M-25 17L-6 0M25 17L6 0" stroke="${INK}" stroke-width="3.5" stroke-linecap="round" opacity=".35"/>
    <path d="M-25.5 -16.5L0 4L25.5 -16.5" fill="#EFE7D6" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
    <circle cx="0" cy="4" r="8.5" fill="#E84A4A" stroke="${INK}" stroke-width="3.5"/>
    <path d="M-3.5 2.5L0 -0.5L3.5 2.5L0 7Z" fill="#FFD1D1"/>`,
  cap: () => `
    <path d="M-26 14H26V24Q26 28 22 28H-22Q-26 28 -26 24Z" fill="#3F8F3E" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
    <path d="M-20 14V6Q-20 2 -16 2H16Q20 2 20 6V14Z" fill="#8A5A3B" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
    <path d="M-7 2V-8H7V2" fill="#C0925F" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
    <ellipse cx="0" cy="-18" rx="16" ry="12" fill="#C9603F" stroke="${INK}" stroke-width="4.5"/>
    <ellipse cx="-5" cy="-22" rx="5" ry="3" fill="#fff" opacity=".6"/>`,
  bendera: () => `
    <path d="M-20 30V-30" stroke="${INK}" stroke-width="6" stroke-linecap="round"/>
    <path d="M-18 -28H26V6H-18Z" fill="#fff" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>
    <rect x="-7" y="-28" width="11" height="11" fill="${INK}"/><rect x="15" y="-28" width="11" height="11" fill="${INK}"/>
    <rect x="-18" y="-17" width="11" height="11" fill="${INK}"/><rect x="4" y="-17" width="11" height="11" fill="${INK}"/>
    <rect x="-7" y="-6" width="11" height="12" fill="${INK}"/><rect x="15" y="-6" width="11" height="12" fill="${INK}"/>`,
  // Tanda tanya abu-abu: zona ulang tutorial (bantuan belajar main).
  tanya: () => `
    <path d="M-13 -11Q-13 -27 1 -27Q15 -27 15 -14Q15 -5 5 0Q0 3 0 10" fill="none" stroke="#5F676C" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>
    <circle cx="0" cy="23" r="6.5" fill="#5F676C"/>`,
  centang: () => `<path d="M-20 0L-6 14L22 -16" fill="none" stroke="#fff" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"/>`,
};

// ---------- zona berhenti ----------
export const PAD_KINDS = {
  paket:   { label: 'Ambil paket',    note: 'Di gudang. Truk berhenti sebentar, paket naik ke bak.', c: '#F09A2C', d: '#C46A12', l: '#FDE9CC', icon: 'paket' },
  rumah:   { label: 'Antar ke rumah', note: 'Di depan pintu rumah tujuan.', c: '#1DB4E8', d: '#0A8DC2', l: '#CDEFFB', icon: 'rumah' },
  bantuan: { label: 'Bantuan warga',  note: 'Misi sampingan: berhenti untuk menolong warga.', c: '#F4C63F', d: '#B98C12', l: '#FBE08A', icon: 'tangan' },
  surat:   { label: 'Taruh surat',    note: 'Tujuan surat dari misi bantuan warga.', c: '#B9583A', d: '#8A3D27', l: '#F6DDD3', icon: 'surat' },
  singgah: { label: 'Singgah',        note: 'Ambil cap di tengah perjalanan antar.', c: '#3F8F3E', d: '#2B652B', l: '#E1F0D6', icon: 'cap' },
  // Permintaan pengguna 26 Sep: area kecil abu-abu di plaza gudang untuk mengulang tutorial (hanya saat tanpa kiriman).
  tutorial: { label: 'Ulang tutorial', note: 'Di plaza gudang. Berhenti di sini untuk mengulang tutorial.', c: '#8E969B', d: '#5F676C', l: '#E3E7EA', icon: 'tanya' },
};

// Jalur persegi bersudut bulat yang dimulai dari tengah atas, searah jarum jam.
export function roundRectPath(x, y, w, h, r) {
  return `M${x + w / 2} ${y}H${x + w - r}A${r} ${r} 0 0 1 ${x + w} ${y + r}V${y + h - r}A${r} ${r} 0 0 1 ${x + w - r} ${y + h}H${x + r}A${r} ${r} 0 0 1 ${x} ${y + h - r}V${y + r}A${r} ${r} 0 0 1 ${x + r} ${y}Z`;
}
const roundRectLen = (w, h, r) => 2 * (w - 2 * r) + 2 * (h - 2 * r) + 2 * Math.PI * r;

// Lingkaran bergerigi (bentuk stempel) untuk opsi B.
function scallopPath(cx, cy, r, bumps, depth) {
  let d = '';
  const steps = bumps * 12;
  for (let i = 0; i <= steps; i++) {
    const a = -Math.PI / 2 + (i / steps) * Math.PI * 2;
    const rr = r - depth * (1 - Math.cos(a * bumps + Math.PI / 2 * 0)) * 0.5;
    d += `${i ? 'L' : 'M'}${f(cx + rr * Math.cos(a))} ${f(cy + rr * Math.sin(a))}`;
  }
  return d + 'Z';
}

// Geometri tepi bantalan A (dipakai juga oleh animasi pratinjau dan, nanti, oleh StopZone di Unity).
export const PAD_RING = { x: 18, w: 220, r: 66 };

// progress 0..1 (null = tanpa lapisan progres). done = tampilan sesaat setelah selesai.
export function padSvg(kind, { shape = 'A', progress = null, done = false, size = 256, ground = null } = {}) {
  const k = PAD_KINDS[kind];
  const bg = ground ? `<rect width="256" height="256" fill="${ground}"/>` : '';
  let body;
  if (shape === 'A') {
    const { x, w, r } = PAD_RING, path = roundRectPath(x, x, w, w, r), len = roundRectLen(w, w, r);
    const inner = roundRectPath(38, 38, 180, 180, 50);
    body = `
      <path d="${path}" fill="${k.c}" fill-opacity=".55"/>
      <path d="${inner}" fill="none" stroke="${k.l}" stroke-width="3" stroke-opacity=".55"/>
      <path d="${path}" fill="none" stroke="#FFFFFF" stroke-width="9" stroke-dasharray="26 17" stroke-linecap="round" stroke-opacity=".92"/>
      ${progress != null ? `<path d="${path}" fill="none" stroke="${k.c}" stroke-width="15" stroke-linecap="round" stroke-dasharray="${f(len * progress)} ${f(len)}"/>
      <path d="${path}" fill="none" stroke="#FFFFFF" stroke-width="4" stroke-linecap="round" stroke-opacity=".55" stroke-dasharray="${f(len * progress)} ${f(len)}"/>` : ''}`;
  } else {
    const path = scallopPath(128, 128, 112, 14, 12);
    const ring = `M128 26A102 102 0 1 1 127.99 26`, len = 2 * Math.PI * 102;
    body = `
      <path d="${path}" fill="${k.c}" fill-opacity=".55"/>
      <path d="${path}" fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linejoin="round" stroke-opacity=".92"/>
      <circle cx="128" cy="128" r="84" fill="none" stroke="${k.l}" stroke-width="3" stroke-dasharray="10 12" stroke-opacity=".7"/>
      ${progress != null ? `<path d="${ring}" fill="none" stroke="${k.c}" stroke-width="14" stroke-linecap="round" stroke-dasharray="${f(len * progress)} ${f(len)}"/>` : ''}`;
  }
  const badgeFill = done ? '#3F8F3E' : '#FFFFFF';
  const icon = done ? ICONS.centang() : ICONS[k.icon]();
  const badge = `
    <circle cx="128" cy="133" r="46" fill="#000" opacity=".22"/>
    <circle cx="128" cy="128" r="46" fill="${badgeFill}" stroke="${k.d}" stroke-width="7"/>
    <g transform="translate(128 128)">${icon}</g>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256">${bg}${body}${badge}</svg>`;
}

// ---------- area selesai kerja: satu area lebar berbentuk kapsul ----------
export function finishAreaSvg({ progress = null, width = 520, ground = null } = {}) {
  const W = 520, H = 280, x = 12, y = 12, w = W - 24, h = H - 24, r = 108;
  const path = roundRectPath(x, y, w, h, r), len = roundRectLen(w, h, r);
  const bg = ground ? `<rect width="${W}" height="${H}" fill="${ground}"/>` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${f(width * H / W)}" viewBox="0 0 ${W} ${H}">${bg}
    <path d="${path}" fill="#1DB4E8" fill-opacity=".2"/>
    <path d="${roundRectPath(34, 34, W - 68, H - 68, 88)}" fill="none" stroke="#FFFFFF" stroke-width="3" stroke-opacity=".75" stroke-dasharray="14 12"/>
    <path d="${path}" fill="none" stroke="#1DB4E8" stroke-width="6"/>
    ${progress != null ? `<path d="${path}" fill="none" stroke="#1DB4E8" stroke-width="12" stroke-linecap="round" stroke-dasharray="${f(len * progress)} ${f(len)}"/>` : ''}
    <g transform="translate(260 140) scale(1.3)">
      <circle r="44" fill="#FFFFFF" fill-opacity=".88"/>
      <g transform="translate(2 0) scale(.95)">${ICONS.bendera()}</g>
    </g>
  </svg>`;
}

// ---------- panah petunjuk (dua warna: ke gudang / ke rumah) ----------
export function arrowSvg(color = '#F09A2C', dark = '#C46A12', size = 128) {
  const d = 'M64 10L110 62Q114 68 106 68H82V110Q82 118 74 118H54Q46 118 46 110V68H22Q14 68 18 62Z';
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 128 128">
    <path d="${d}" transform="translate(0 5)" fill="#000" opacity=".22"/>
    <path d="${d}" fill="${color}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
    <path d="M82 70V110Q82 118 74 118H64V70Z M64 14L106 62Q110 66 104 66H64Z" fill="${dark}" opacity=".55"/>
    <path d="M60 24L32 56" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round" opacity=".75"/>
  </svg>`;
}

// ---------- pickup ----------
function starPath(cx, cy, r, inner = 0.5, round = 0) {
  let d = '';
  for (let i = 0; i < 10; i++) {
    const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? r * inner : r;
    d += `${i ? 'L' : 'M'}${f(cx + rr * Math.cos(a))} ${f(cy + rr * Math.sin(a))}`;
  }
  return d + 'Z';
}
const sparkle = (x, y, s, c = '#FFFFFF') =>
  `<path d="M${x} ${y - s}Q${x} ${y} ${x + s} ${y}Q${x} ${y} ${x} ${y + s}Q${x} ${y} ${x - s} ${y}Q${x} ${y} ${x} ${y - s}Z" fill="${c}" stroke="${INK}" stroke-width="2.5" stroke-linejoin="round"/>`;

// Pickup gaya stiker (revisi Gerbang 4): pinggiran putih tebal di luar outline tinta supaya terbaca
// sebagai barang yang bisa diambil di atas aspal maupun rumput. Warna kilau di bawahnya: PICKUP_GLOW.
const RIM = 28; // lebar goresan putih; tinta 10 di atasnya
const rimmed = (d, fill, extra = '') => `<path d="${d}" fill="#fff" stroke="#fff" stroke-width="${RIM}" stroke-linejoin="round"${extra}/><path d="${d}" fill="${fill}" stroke="${INK}" stroke-width="10" stroke-linejoin="round"${extra}/>`;
const SHIELD = 'M128 34L208 62V122C208 170 173 204 128 224C83 204 48 170 48 122V62Z';
export const PICKUP_GLOW = { bintang: '#F4C63F', kilat: '#1DB4E8', perisai: '#5DBB4F' };
export const PICKUPS = {
  bintang: {
    // Digeser 6 px ke bawah supaya kotak batas bintang tepat di tengah kilau (bintang lebih berat di atas).
    label: 'Bintang', note: 'Koin bonus di jalan. Wajah tersenyum supaya ramah anak.',
    draw: () => `
      <g transform="translate(0 6)">${rimmed(starPath(128, 132, 104, 0.52), '#F4C63F')}
      <path d="${starPath(128, 136, 74, 0.52)}" fill="#FBE08A" opacity=".8"/>
      <path d="M92 86L112 74" stroke="#fff" stroke-width="9" stroke-linecap="round"/>
      <circle cx="108" cy="132" r="9" fill="${INK}"/><circle cx="148" cy="132" r="9" fill="${INK}"/>
      <circle cx="111" cy="128" r="3" fill="#fff"/><circle cx="151" cy="128" r="3" fill="#fff"/>
      <path d="M113 153Q128 166 143 153" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>
      <ellipse cx="94" cy="150" rx="9" ry="6" fill="#F09A2C" opacity=".6"/><ellipse cx="162" cy="150" rx="9" ry="6" fill="#F09A2C" opacity=".6"/>
      </g>${sparkle(218, 40, 17)}${sparkle(34, 214, 12)}`,
  },
  kilat: {
    label: 'Kilat', note: 'Bantuan cepat: truk melaju lebih kencang sebentar.',
    draw: () => `
      <circle cx="128" cy="128" r="100" fill="#fff" stroke="#fff" stroke-width="${RIM}"/>
      <circle cx="128" cy="128" r="100" fill="#1DB4E8" stroke="${INK}" stroke-width="10"/>
      <circle cx="128" cy="128" r="76" fill="#0A8DC2"/>
      <path d="M84 70A68 68 0 0 1 150 62" fill="none" stroke="#7FD7F5" stroke-width="9" stroke-linecap="round"/>
      <path d="M144 42L76 140H122L104 214L180 104H132Z" fill="#fff" stroke="#fff" stroke-width="22" stroke-linejoin="round"/>
      <path d="M144 42L76 140H122L104 214L180 104H132Z" fill="#F4C63F" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>
      <path d="M138 64L100 124" stroke="#FFF6CF" stroke-width="7" stroke-linecap="round"/>`,
  },
  perisai: {
    label: 'Perisai', note: 'Melindungi paket rapuh dari satu benturan.',
    draw: () => `
      ${rimmed(SHIELD, '#3F8F3E')}
      <path d="M128 56L190 78V122C190 160 164 188 128 204Z" fill="#2B652B" opacity=".55"/>
      <path d="M128 56L66 78V122C66 146 76 164 92 178" fill="none" stroke="#9FD38F" stroke-width="8" stroke-linecap="round" opacity=".9"/>
      <g transform="translate(128 126) scale(1.35)">${ICONS.paket()}</g>`,
  },
  singgah: {
    label: 'Singgah', note: 'Titik cap di tengah perjalanan (dulu kepala monster).',
    draw: () => `
      <circle cx="128" cy="136" r="92" fill="#000" opacity=".2"/>
      <circle cx="128" cy="126" r="92" fill="#E1F0D6" stroke="${INK}" stroke-width="9"/>
      <circle cx="128" cy="126" r="72" fill="none" stroke="#3F8F3E" stroke-width="6" stroke-dasharray="14 10"/>
      <g transform="translate(128 126) scale(1.9)">${ICONS.cap()}</g>`,
  },
};

export const pickupSvg = (name, size = 256) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256">${PICKUPS[name].draw()}</svg>`;

// Kilau di bawah pickup: putih supaya bisa diwarnai di Unity.
// Tanpa warna: putih (diwarnai di Unity dengan PICKUP_GLOW). Pratinjau memakai versi berwarna.
export const glowSvg = (size = 256, c = '#fff') => `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256">
  <defs><radialGradient id="g"><stop offset="0" stop-color="${c}" stop-opacity=".85"/><stop offset=".5" stop-color="${c}" stop-opacity=".55"/><stop offset="1" stop-color="${c}" stop-opacity="0"/></radialGradient></defs>
  <circle cx="128" cy="128" r="126" fill="url(#g)"/>
  <circle cx="128" cy="128" r="104" fill="none" stroke="#fff" stroke-width="7" stroke-dasharray="4 18" stroke-linecap="round" opacity=".95"/>
</svg>`;
