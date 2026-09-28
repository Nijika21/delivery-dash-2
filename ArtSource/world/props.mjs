// Properti kota Delivery Dash 2. Sudut pandang dan garis tinta sama dengan rumah (kanvas 256x256).
import { INK } from '../houses/houses.mjs';

const WOOD = '#9A6A45', WOOD_D = '#6F4A2F', WOOD_L = '#C0925F', LEAF = '#5E9E4B', LEAF_D = '#3F7A36', STONE = '#B9B8A8', STONE_D = '#8F8E80';
const shadow = (cx, cy, rx, ry = rx * 0.22) => `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="#000" opacity=".2"/>`;
const flower = (x, y, c, r = 7) => `<circle cx="${x}" cy="${y}" r="${r}" fill="${c}" stroke="${INK}" stroke-width="2.5"/><circle cx="${x}" cy="${y}" r="${r * 0.35}" fill="#F4C63F"/>`;

export const PROPS = {
  lampu: {
    label: 'Lampu jalan', note: 'Tiang tunggal, cahaya hangat.',
    draw: () => `
      ${shadow(128, 236, 34)}
      <path d="M112 236H144L138 220H118Z" fill="#3A4A50" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
      <rect x="121" y="84" width="14" height="138" rx="4" fill="#4D6168" stroke="${INK}" stroke-width="5"/>
      <path d="M128 90Q128 58 158 58" fill="none" stroke="${INK}" stroke-width="15" stroke-linecap="round"/>
      <path d="M128 90Q128 58 158 58" fill="none" stroke="#4D6168" stroke-width="6" stroke-linecap="round"/>
      <circle cx="170" cy="98" r="40" fill="#FBE08A" opacity=".35"/>
      <path d="M146 58H194L184 82H156Z" fill="#33464C" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
      <path d="M156 82H184Q182 96 170 96Q158 96 156 82Z" fill="#FFE9A3" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>
      <rect x="124" y="120" width="8" height="70" rx="3" fill="#7F959B" opacity=".6"/>`,
  },
  batu: {
    label: 'Batu', note: 'Kelompok batu bulat, dengan lumut.',
    draw: () => `
      ${shadow(128, 214, 90)}
      <path d="M58 212Q40 212 42 190Q46 150 88 142Q120 136 136 160Q146 176 140 200Q136 212 118 212Z" fill="${STONE}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      <path d="M60 170Q72 152 96 150" fill="none" stroke="#DAD9CB" stroke-width="6" stroke-linecap="round"/>
      <path d="M140 212Q128 212 128 196Q130 170 162 166Q196 164 206 188Q212 212 192 212Z" fill="${STONE_D}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      <path d="M146 186Q156 176 172 176" fill="none" stroke="${STONE}" stroke-width="5" stroke-linecap="round"/>
      <path d="M84 146Q96 132 112 142Q104 150 84 146Z" fill="${LEAF}" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>
      <path d="M34 214Q40 196 50 214M210 214Q214 200 222 214" fill="${LEAF}" stroke="${INK}" stroke-width="3"/>`,
  },
  kotakSurat: {
    label: 'Kotak surat', note: 'Juga dipakai sebagai tanda tujuan misi surat.',
    draw: () => `
      ${shadow(128, 234, 40)}
      <rect x="120" y="140" width="16" height="94" rx="3" fill="${WOOD}" stroke="${INK}" stroke-width="5"/>
      <path d="M78 150V106Q78 72 112 72H148Q182 72 182 106V150Z" fill="#C9603F" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      <path d="M92 112Q92 88 112 88H146" fill="none" stroke="#E3835F" stroke-width="6" stroke-linecap="round"/>
      <rect x="96" y="118" width="46" height="10" rx="5" fill="${INK}"/>
      <path d="M182 86V56" stroke="${INK}" stroke-width="5" stroke-linecap="round"/>
      <path d="M184 56H212V76H184Z" fill="#F4C63F" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>`,
  },
  bangku: {
    label: 'Bangku', note: 'Bangku taman kayu.',
    draw: () => `
      ${shadow(128, 212, 96)}
      <rect x="46" y="170" width="12" height="42" fill="#4D6168" stroke="${INK}" stroke-width="5"/>
      <rect x="198" y="170" width="12" height="42" fill="#4D6168" stroke="${INK}" stroke-width="5"/>
      <rect x="36" y="100" width="184" height="22" rx="6" fill="${WOOD}" stroke="${INK}" stroke-width="6"/>
      <rect x="36" y="128" width="184" height="22" rx="6" fill="${WOOD}" stroke="${INK}" stroke-width="6"/>
      <rect x="30" y="156" width="196" height="24" rx="7" fill="${WOOD_L}" stroke="${INK}" stroke-width="6"/>
      <path d="M48 108H206M48 136H206M44 164H212" stroke="#fff" stroke-width="3" opacity=".3" stroke-linecap="round"/>`,
  },
  bedeng: {
    label: 'Bedeng bunga', note: 'Kotak tanam dengan bunga warna-warni.',
    draw: () => `
      ${shadow(128, 208, 104)}
      <path d="M30 150H226L216 206H40Z" fill="${WOOD}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      <path d="M34 168H222" stroke="${WOOD_D}" stroke-width="4"/>
      <path d="M36 150Q46 118 70 132Q84 104 108 126Q128 100 150 124Q172 102 188 128Q210 116 220 150Z" fill="${LEAF}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
      <path d="M64 142Q76 128 92 136M146 136Q160 124 176 136" fill="none" stroke="${LEAF_D}" stroke-width="4" stroke-linecap="round"/>
      ${flower(62, 126, '#FF8FA3', 10)}${flower(98, 112, '#FFFFFF', 10)}${flower(132, 104, '#F09A2C', 11)}${flower(166, 114, '#FF8FA3', 10)}${flower(200, 128, '#9C7BD6', 10)}
      ${flower(116, 132, '#E84A4A', 8)}${flower(184, 140, '#FFFFFF', 8)}`,
  },
  pagar: {
    label: 'Pagar', note: 'Pagar kayu putih, bisa disambung.',
    draw: () => {
      let pick = '';
      for (let i = 0; i < 7; i++) {
        const x = 18 + i * 34;
        pick += `<path d="M${x} 216V120L${x + 12} 104L${x + 24} 120V216Z" fill="#FFFDF6" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
          <path d="M${x + 18} 124V210" stroke="#D5DEDC" stroke-width="5"/>`;
      }
      return `${shadow(128, 218, 116, 10)}
        <rect x="10" y="136" width="236" height="14" rx="4" fill="#E9EEEA" stroke="${INK}" stroke-width="5"/>
        <rect x="10" y="182" width="236" height="14" rx="4" fill="#E9EEEA" stroke="${INK}" stroke-width="5"/>${pick}`;
    },
  },
  dermaga: {
    label: 'Dermaga', note: 'Untuk tepi danau Kampung Telaga (air digambar di tanah, bukan di sprite).',
    draw: () => `
      ${shadow(128, 212, 100, 12)}
      ${[70, 186].map(x => `<rect x="${x - 7}" y="150" width="14" height="80" rx="4" fill="${WOOD_D}" stroke="${INK}" stroke-width="5"/>`).join('')}
      <path d="M44 60H212V176H44Z" fill="${WOOD}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
      ${[0, 1, 2, 3, 4].map(i => `<path d="M48 ${80 + i * 20}H208" stroke="${WOOD_D}" stroke-width="4"/>`).join('')}
      <path d="M52 66H204" stroke="${WOOD_L}" stroke-width="5" stroke-linecap="round"/>
      ${[56, 200].map(x => `<rect x="${x - 10}" y="38" width="20" height="30" rx="6" fill="${WOOD_D}" stroke="${INK}" stroke-width="5"/>`).join('')}
      <circle cx="200" cy="120" r="14" fill="none" stroke="#E84A4A" stroke-width="8"/>
      <circle cx="200" cy="120" r="14" fill="none" stroke="#fff" stroke-width="8" stroke-dasharray="11 11"/>
      <circle cx="200" cy="120" r="19" fill="none" stroke="${INK}" stroke-width="3"/><circle cx="200" cy="120" r="9" fill="none" stroke="${INK}" stroke-width="3"/>`,
  },
  lapak: {
    label: 'Lapak pasar', note: 'Untuk kawasan Pasar & Sekolah.',
    draw: () => {
      let roof = '';
      for (let i = 0; i < 6; i++) roof += `<path d="M${32 + i * 32} 92h32v22a16 16 0 0 1 -32 0z" fill="${i % 2 ? '#FFFDF6' : '#3F8F3E'}" stroke="${INK}" stroke-width="4.5" stroke-linejoin="round"/>`;
      return `${shadow(128, 230, 104)}
        <rect x="40" y="96" width="10" height="134" fill="${WOOD}" stroke="${INK}" stroke-width="4"/>
        <rect x="206" y="96" width="10" height="134" fill="${WOOD}" stroke="${INK}" stroke-width="4"/>
        <path d="M24 94L52 50H204L232 94Z" fill="#3F8F3E" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>
        <path d="M60 60H196" stroke="#79BC6E" stroke-width="5" stroke-linecap="round"/>
        ${roof}
        <rect x="32" y="166" width="192" height="50" rx="6" fill="${WOOD_L}" stroke="${INK}" stroke-width="6"/>
        <path d="M36 180H220" stroke="${WOOD}" stroke-width="4"/>
        <path d="M44 166Q60 140 84 166Z" fill="#E84A4A" stroke="${INK}" stroke-width="4"/>
        ${[0, 1, 2, 3].map(i => `<circle cx="${104 + i * 16}" cy="${158 - (i % 2) * 6}" r="10" fill="#F09A2C" stroke="${INK}" stroke-width="3.5"/>`).join('')}
        <path d="M172 166Q180 138 200 146Q214 150 208 166Z" fill="#7FB65A" stroke="${INK}" stroke-width="4"/>
        <path d="M188 150Q192 140 200 138" fill="none" stroke="${INK}" stroke-width="3"/>`;
    },
  },
  taman: {
    label: 'Taman bermain', note: 'Perosotan dan ayunan di Taman Bukit.',
    draw: () => `
      ${shadow(128, 228, 112)}
      <path d="M20 228L60 72M100 228L60 72" stroke="#E84A4A" stroke-width="12" stroke-linecap="round"/>
      <path d="M20 228L60 72M100 228L60 72" stroke="${INK}" stroke-width="3" stroke-linecap="round" opacity=".4"/>
      <path d="M40 70H120" stroke="${INK}" stroke-width="15" stroke-linecap="round"/><path d="M40 70H120" stroke="#F4C63F" stroke-width="8" stroke-linecap="round"/>
      <path d="M72 74V166M100 74V166" stroke="${INK}" stroke-width="3.5"/>
      <rect x="64" y="164" width="44" height="10" rx="4" fill="#1DB4E8" stroke="${INK}" stroke-width="4"/>
      <path d="M150 228V110H178V228" fill="none" stroke="${INK}" stroke-width="5"/>
      ${[128, 150, 172, 194, 216].map(y => `<path d="M150 ${y}H178" stroke="${INK}" stroke-width="5"/>`).join('')}
      <rect x="140" y="96" width="48" height="16" rx="5" fill="#1DB4E8" stroke="${INK}" stroke-width="5"/>
      <path d="M184 104Q208 110 216 160Q222 206 244 222L236 232Q208 214 202 164Q196 124 180 116Z" fill="#F4C63F" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/>
      <path d="M194 116Q206 130 210 164" fill="none" stroke="#FFF6CF" stroke-width="4" stroke-linecap="round"/>`,
  },
  // Tampak atas (datar di tanah), bukan tampak depan: dipasang di tengah Bundaran Kota. Warna mengikuti air mancur lama.
  airMancur: {
    label: 'Air mancur', note: 'Pusat Bundaran Kota, digambar ulang dari air mancur kota lama.', flat: true,
    draw: () => {
      const C = 128, ring = (r, sw, c, o = 1) => `<circle cx="${C}" cy="${C}" r="${r}" fill="none" stroke="${c}" stroke-width="${sw}" opacity="${o}"/>`;
      const tiles = Array.from({ length: 16 }, (_, i) => {
        const a = i * Math.PI / 8, c = Math.cos(a), s = Math.sin(a);
        return `M${(C + c * 100).toFixed(1)} ${(C + s * 100).toFixed(1)}L${(C + c * 122).toFixed(1)} ${(C + s * 122).toFixed(1)}`;
      }).join('');
      const drops = Array.from({ length: 8 }, (_, i) => {
        const a = i * Math.PI / 4 + Math.PI / 8, r = 43;
        return `<circle cx="${(C + Math.cos(a) * r).toFixed(1)}" cy="${(C + Math.sin(a) * r).toFixed(1)}" r="4.5" fill="#EAF7F6" stroke="${INK}" stroke-width="2"/>`;
      }).join('');
      return `
      <circle cx="${C}" cy="${C}" r="122" fill="#E6DFC6" stroke="${INK}" stroke-width="6"/>
      <path d="${tiles}" stroke="#C9C1A5" stroke-width="3"/>
      ${ring(111, 2, '#C9C1A5')}
      <circle cx="${C}" cy="${C}" r="100" fill="#CCC3A8" stroke="${INK}" stroke-width="6"/>
      <circle cx="${C}" cy="${C}" r="84" fill="#569AA2" stroke="${INK}" stroke-width="5"/>
      ${ring(70, 5, '#74B3B4')}${ring(52, 5, '#8BC0C0', 0.9)}
      <path d="M86 92Q100 76 122 72" fill="none" stroke="#B8E0DF" stroke-width="6" stroke-linecap="round" opacity=".8"/>
      <circle cx="${C}" cy="${C}" r="30" fill="#DFD8B7" stroke="${INK}" stroke-width="5"/>
      <circle cx="${C}" cy="${C}" r="20" fill="#74B3B4" stroke="${INK}" stroke-width="3.5"/>
      ${drops}
      <circle cx="${C}" cy="${C}" r="9" fill="#EFE9D6" stroke="${INK}" stroke-width="4"/>
      <circle cx="${C - 2}" cy="${C - 3}" r="3" fill="#fff"/>`;
    },
  },
  papanSekolah: {
    label: 'Papan sekolah', note: 'Penanda kawasan sekolah, tanpa tulisan (ikon buku dan pensil).',
    draw: () => `
      ${shadow(128, 232, 70)}
      <rect x="78" y="150" width="14" height="82" fill="${WOOD}" stroke="${INK}" stroke-width="5"/>
      <rect x="164" y="150" width="14" height="82" fill="${WOOD}" stroke="${INK}" stroke-width="5"/>
      <rect x="42" y="60" width="172" height="100" rx="14" fill="#3F8F3E" stroke="${INK}" stroke-width="7"/>
      <rect x="54" y="72" width="148" height="76" rx="8" fill="#2B652B"/>
      <path d="M92 90Q110 82 126 92V132Q110 122 92 130Z" fill="#FFFDF6" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>
      <path d="M160 90Q142 82 126 92V132Q142 122 160 130Z" fill="#CDEFFB" stroke="${INK}" stroke-width="4" stroke-linejoin="round"/>
      <path d="M170 128L186 84L196 88L180 132L170 136Z" fill="#F4C63F" stroke="${INK}" stroke-width="3.5" stroke-linejoin="round"/>
      <path d="M186 84L190 74L200 78L196 88" fill="#FF8FA3" stroke="${INK}" stroke-width="3.5" stroke-linejoin="round"/>`,
  },
};

export const propSvg = (name, size = 256) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256">${PROPS[name].draw()}</svg>`;
