// Depot Delivery Dash 2: gudang bergaris tinta dengan kedalaman (atap lengkung, kanopi, dok muat),
// dan pelataran berpaving dengan zona ambil paket serta parkir selesai kerja.
// Warna turunan depot lama: atap bata, dinding krem, pintu gelap, ambang kuning.
import { INK } from '../houses/houses.mjs';
import { ICONS, padSvg, finishAreaSvg } from './markers.mjs';

const ROOF = '#B85A3A', ROOF_D = '#8A3F27', ROOF_L = '#D9805C';
const WALL = '#EADFBD', WALL_D = '#D3C49A', DOOR = '#3D5254', DOOR_L = '#57706F', SILL = '#F4C63F';
const CONCRETE = '#CFD1C8', CONCRETE_D = '#B7B9AF', PAVE = '#D9DBD2', PAVE_SEAM = '#C3C5BB';
const f = n => +n.toFixed(1);

// Titik pada kurva kuadrat (untuk rusuk atap).
const q = (p0, c, p1, t) => [(1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p1[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p1[1]];

function box(x, y, s = 1) {
  return `<g transform="translate(${x} ${y}) scale(${s})">
    <path d="M0 -30L28 -16V16L0 30L-28 16V-16Z" fill="none" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>${ICONS.paket()}</g>`;
}

// Gedung gudang, kanvas 512x300. Sisi bawah = muka gudang (menghadap pelataran).
export function depotBuildingSvg(size = 512) {
  const L = [36, 150], R = [476, 150], C = [256, -20];          // lengkung muka
  const BL = [36, 104], BR = [476, 104], BC = [256, -66];       // lengkung belakang (kedalaman atap)
  let ribs = '';
  for (let i = 1; i < 12; i++) {
    const t = i / 12, a = q(L, C, R, t), b = q(BL, BC, BR, t);
    ribs += `<path d="M${f(a[0])} ${f(a[1])}L${f(b[0])} ${f(b[1])}" stroke="${i % 2 ? ROOF_D : ROOF_L}" stroke-width="${i % 2 ? 4 : 3}" stroke-linecap="round"/>`;
  }
  const doorX = [74, 212, 350], doorW = 88;
  const doors = doorX.map((x, i) => {
    const open = i === 1;
    let slats = '';
    for (let y = 176; y < (open ? 204 : 246); y += 9) slats += `<path d="M${x + 6} ${y}H${x + doorW - 6}" stroke="${DOOR_L}" stroke-width="3"/>`;
    return `
      <path d="M${x} 252V178Q${x} 168 ${x + 10} 168H${x + doorW - 10}Q${x + doorW} 168 ${x + doorW} 178V252Z" fill="${open ? '#1E2A2B' : DOOR}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      ${open ? `<path d="M${x + 3} 208H${x + doorW - 3}V171H${x + 3}Z" fill="${DOOR}"/>
        <path d="M${x + 3} 208H${x + doorW - 3}" stroke="${INK}" stroke-width="4"/>
        ${box(x + 28, 234, 0.42)}${box(x + 58, 238, 0.36)}${box(x + 44, 218, 0.34)}` : ''}
      ${slats}
      <rect x="${x - 2}" y="248" width="${doorW + 4}" height="7" rx="2" fill="${SILL}" stroke="${INK}" stroke-width="3"/>
      <circle cx="${x + doorW / 2}" cy="160" r="5" fill="#FFE9A3" stroke="${INK}" stroke-width="2.5"/>`;
  }).join('');
  let hazard = '';
  for (let x = 30; x < 482; x += 18) hazard += `<path d="M${x} 284L${x + 9} 284L${x + 17} 276L${x + 8} 276Z" fill="${INK}"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${f(size * 300 / 512)}" viewBox="0 0 512 300">
    <path d="M${L[0]} ${L[1]}Q${C[0]} ${C[1]} ${R[0]} ${R[1]}L${BR[0]} ${BR[1]}Q${BC[0]} ${BC[1]} ${BL[0]} ${BL[1]}Z" fill="${ROOF}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
    ${ribs}
    <g transform="translate(150 40)"><rect x="-12" y="-4" width="24" height="18" rx="3" fill="#9AA7A8" stroke="${INK}" stroke-width="4"/><path d="M-14 -4Q0 -22 14 -4Z" fill="#C6CFD0" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/></g>
    <g transform="translate(362 40)"><rect x="-12" y="-4" width="24" height="18" rx="3" fill="#9AA7A8" stroke="${INK}" stroke-width="4"/><path d="M-14 -4Q0 -22 14 -4Z" fill="#C6CFD0" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/></g>
    <path d="M${L[0]} 262V${L[1]}Q${C[0]} ${C[1]} ${R[0]} ${R[1]}V262Z" fill="${WALL}"/>
    <path d="M44 150Q256 -8 468 150" fill="none" stroke="${WALL_D}" stroke-width="14"/>
    <rect x="39" y="232" width="434" height="30" fill="${WALL_D}"/>
    <g transform="translate(256 104)">
      <circle r="44" fill="#FFFDF6" stroke="${INK}" stroke-width="7"/>
      <circle r="35" fill="#1DB4E8"/>
      <g transform="scale(.95)">${ICONS.paket()}</g>
      <path d="M-26 -18A33 33 0 0 1 4 -34" fill="none" stroke="#fff" stroke-width="5" stroke-linecap="round" opacity=".6"/>
    </g>
    <path d="M60 158H452" stroke="${INK}" stroke-width="16" stroke-linecap="round"/>
    <path d="M60 158H452" stroke="#F09A2C" stroke-width="9" stroke-linecap="round"/>
    <path d="M64 166H448" stroke="#000" stroke-width="5" opacity=".12"/>
    ${doors}
    <path d="M${L[0]} 262V${L[1]}Q${C[0]} ${C[1]} ${R[0]} ${R[1]}V262Z" fill="none" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
    <path d="M${L[0] - 14} ${L[1] + 6}Q${C[0]} ${C[1] - 16} ${R[0] + 14} ${R[1] + 6}" fill="none" stroke="${INK}" stroke-width="30" stroke-linecap="round"/>
    <path d="M${L[0] - 14} ${L[1] + 6}Q${C[0]} ${C[1] - 16} ${R[0] + 14} ${R[1] + 6}" fill="none" stroke="${ROOF}" stroke-width="18" stroke-linecap="round"/>
    <path d="M${L[0] + 6} ${L[1] - 12}Q${C[0]} ${C[1] - 30} ${R[0] - 6} ${R[1] - 12}" fill="none" stroke="${ROOF_L}" stroke-width="4" stroke-linecap="round"/>
    <rect x="24" y="258" width="464" height="26" rx="5" fill="${CONCRETE}" stroke="${INK}" stroke-width="6"/>
    <path d="M28 266H484" stroke="#fff" stroke-width="3" opacity=".45"/>
    <rect x="27" y="276" width="458" height="8" fill="${SILL}"/>${hazard}
    <rect x="24" y="258" width="464" height="26" rx="5" fill="none" stroke="${INK}" stroke-width="6"/>
    ${box(40, 236, 0.5)}${box(472, 236, 0.5)}${box(456, 214, 0.42)}
  </svg>`;
}

// Pot tanaman dan tiang pembatas kecil untuk pelataran (tampak atas-depan).
const planter = (x, y, w = 70) => `<g transform="translate(${x} ${y})">
  <ellipse cx="${w / 2}" cy="34" rx="${w / 2 + 4}" ry="7" fill="#000" opacity=".18"/>
  <rect x="0" y="12" width="${w}" height="22" rx="6" fill="#9A6A45" stroke="${INK}" stroke-width="4.5"/>
  <path d="M4 14Q${w * 0.2} -6 ${w * 0.4} 8Q${w * 0.55} -8 ${w * 0.7} 8Q${w * 0.85} -4 ${w - 4} 14Z" fill="#5E9E4B" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>
  <circle cx="${w * 0.3}" cy="4" r="4" fill="#FF8FA3" stroke="${INK}" stroke-width="2"/><circle cx="${w * 0.66}" cy="2" r="4" fill="#fff" stroke="${INK}" stroke-width="2"/></g>`;
const bollard = (x, y) => `<g transform="translate(${x} ${y})"><ellipse cx="0" cy="14" rx="9" ry="3" fill="#000" opacity=".2"/>
  <rect x="-6" y="-6" width="12" height="20" rx="5" fill="${SILL}" stroke="${INK}" stroke-width="3.5"/><path d="M-5 2H5" stroke="${INK}" stroke-width="3"/></g>`;

// Halaman depot lengkap (pengganti persegi aspal gelap 24x16 lama). 1 unit ≈ 30 px.
// Paving terang berpola ubin, trotoar melengkung, mulut halaman melebar ke jalan di bawah.
// Semua hiasan ada di tepi supaya bagian tengah tetap lega untuk menyetir.
export function depotPlazaSvg({ width = 760, decor = '' } = {}) {
  const W = 760, H = 540, TOP = 100, SIDE = 40, ROAD = 468, R = 64, F = 40;
  const plaza = `M0 ${ROAD}A${F} ${F} 0 0 0 ${SIDE} ${ROAD - F}V${TOP + R}A${R} ${R} 0 0 1 ${SIDE + R} ${TOP}H${W - SIDE - R}A${R} ${R} 0 0 1 ${W - SIDE} ${TOP + R}V${ROAD - F}A${F} ${F} 0 0 0 ${W} ${ROAD}Z`;
  let seams = '';
  for (let x = 20; x < W; x += 40) seams += `<path d="M${x} ${TOP}V${ROAD}" stroke="${PAVE_SEAM}" stroke-width="2"/>`;
  for (let y = TOP + 20; y < ROAD; y += 40) seams += `<path d="M0 ${y}H${W}" stroke="${PAVE_SEAM}" stroke-width="2"/>`;
  const inner = s => s.replace(/^<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
  const arrow = (x, y, rot) => `<path d="M0 -26L18 -6H7V24H-7V-6H-18Z" transform="translate(${x} ${y}) rotate(${rot})" fill="#fff" opacity=".8"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${f(width * H / W)}" viewBox="0 0 ${W} ${H}">
    <defs><clipPath id="halaman"><path d="${plaza}"/></clipPath></defs>
    <rect width="${W}" height="${H}" fill="#7FA85A"/>
    <path d="${plaza}" fill="none" stroke="#9A9B92" stroke-width="22"/>
    <path d="${plaza}" fill="none" stroke="#EFE9D6" stroke-width="12"/>
    <path d="${plaza}" fill="${PAVE}"/>
    <g clip-path="url(#halaman)">
      ${seams}
      <path d="${plaza}" fill="none" stroke="${CONCRETE_D}" stroke-width="26" opacity=".55"/>
      <path d="M150 336H620" stroke="#000" stroke-width="22" opacity=".08"/>
    </g>
    <rect x="-10" y="${ROAD - 6}" width="${W + 20}" height="${H - ROAD + 6}" fill="#2B2E2C"/>
    <path d="M0 ${ROAD + 38}H${W}" stroke="#A8A998" stroke-width="5" stroke-dasharray="30 22"/>
    ${arrow(118, 420, 0)}${arrow(640, 300, 180)}
    <g transform="translate(124 36)">${inner(depotBuildingSvg())}</g>
    ${bollard(142, 322)}${bollard(618, 322)}
    <g transform="translate(328 334) scale(.41)">${inner(padSvg('paket'))}</g>
    <g transform="translate(474 334) scale(${f(236 / 520)})">${inner(finishAreaSvg())}</g>
    ${planter(52, 156, 58)}${planter(52, 236, 58)}
    ${decor}
  </svg>`;
}

// Halaman depot lama untuk perbandingan, dari koordinat DeliveryTown.BuildDepot (30 px per unit):
// halaman Rect(-12,-16,24,16) aspal gelap, gudang Rect(-11,-15,10,5) di kiri bawah dengan pintu ke utara.
export function oldDepotSvg({ width = 760 } = {}) {
  const W = 760, H = 540, sx = x => (x + 12) * 30 + 20, sy = y => -y * 30 + 50;
  const rect = (x, y, w, h, fill, extra = '') => `<rect x="${sx(x)}" y="${sy(y + h)}" width="${w * 30}" height="${h * 30}" fill="${fill}"${extra}/>`;
  const label = (x, y, s, fill, t) => `<text x="${sx(x)}" y="${sy(y)}" text-anchor="middle" dominant-baseline="middle" font-family="Fredoka, sans-serif" font-weight="700" font-size="${s * 30}" fill="${fill}">${t}</text>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${f(width * H / W)}" viewBox="0 0 ${W} ${H}">
    <rect width="${W}" height="${H}" fill="#7FA85A"/>
    ${rect(-12.45, -16.45, 24.9, 16.9, '#9A9B92')}${rect(-12.2, -16.2, 24.4, 16.4, '#F0EBDA')}
    ${rect(-12, -16, 24, 16, '#282A28')}
    ${rect(-11.2, -15.3, 10.4, 5.6, '#444C36')}${rect(-11, -15, 10, 5, '#DED0A5')}${rect(-11, -15, 10, 3.5, '#AA5837')}
    ${Array.from({ length: 10 }, (_, i) => rect(-10.5 + i, -14.8, 0.07, 3.1, '#89402D')).join('')}
    ${[0, 1, 2].map(i => rect(-10 + i * 3, -11.4, 2, 1.4, '#344541') + rect(-10 + i * 3, -10.15, 2, 0.15, '#F4CA4B')).join('')}
    ${label(-6, -12.9, 0.46, '#fff', 'GUDANG PAKET')}
    <circle cx="${sx(-5)}" cy="${sy(-6)}" r="36" fill="#F09A2C" fill-opacity=".3"/>
    ${label(-5, -4.3, 0.34, '#F7D565', 'AMBIL DI SINI')}
    ${label(5, -3, 0.4, '#E6E0C8', 'HALAMAN SORTIR')}
    ${[1.75, 6.25].map(x => rect(x, -14.5, 4, 4.5, '#3DA8F2', ' fill-opacity=".05" stroke="#3DA8F2" stroke-width="2.5"')).join('')}
  </svg>`;
}

// Warna paving halaman depot, dipakai juga oleh halaman pratinjau.
export const DEPOT_GROUND = { pave: PAVE, seam: PAVE_SEAM };
