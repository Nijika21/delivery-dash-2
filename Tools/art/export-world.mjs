// Ekspor sprite dunia (I3) dari sumber vektor ArtSource ke Unity.
// Pemakaian: node Tools/art/export-world.mjs
//   Publik     → Assets/Art/World/{Rumah,Properti,Depot,Pickup,Panah}/*.png (satu atlas)
//                Assets/Art/World/Zona/*.png (tanpa atlas: shader isi-zona butuh UV penuh 0..1)
// Pohon, pohon2, dan semak tetap memakai gambar asli (DeliveryContentCatalog.propSprites).
// Nama file = id yang dicari runtime (WorldArt), jadi jangan diganti tanpa mengubah kodenya.
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { writeSvgPng } from './svg.mjs';
import { houseSvg, HOUSE_TYPES, COLORWAYS } from '../../ArtSource/houses/houses.mjs';
import { PROPS, propSvg } from '../../ArtSource/world/props.mjs';
import { depotBuildingSvg } from '../../ArtSource/world/depot.mjs';
import { PAD_KINDS, ICONS, padSvg, finishAreaSvg, arrowSvg, PICKUP_GLOW, pickupSvg, glowSvg } from '../../ArtSource/world/markers.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PUB = join(ROOT, 'Assets/Art/World');
let n = 0;
const out = (path, svg, width) => { writeSvgPng(path, svg, width); n++; };

// Rumah: 6×6 unit di dunia. 320 px ≈ 53 px/unit (layar 720p landscape ≈ 55 px/unit).
for (const t of HOUSE_TYPES) for (const cw of Object.keys(COLORWAYS)) out(join(PUB, 'Rumah', `${t}-${cw}.png`), houseSvg(t, cw), 320);
out(join(PUB, 'Rumah', 'tutorial.png'), houseSvg('tutorial', 'laut'), 320);

// Properti baru (airMancur datar 18 unit → 1024 px).
for (const k of Object.keys(PROPS)) out(join(PUB, 'Properti', `${k}.png`), propSvg(k), k === 'airMancur' ? 1024 : 256);

// Gedung gudang (kanvas 512×300; di dunia tinggi = warehouse.size.y).
out(join(PUB, 'Depot', 'gudang.png'), depotBuildingSvg(), 1024);

// Pickup stiker + kilau berwarna.
for (const k of ['bintang', 'kilat', 'perisai']) {
  out(join(PUB, 'Pickup', `${k}.png`), pickupSvg(k), 256);
  out(join(PUB, 'Pickup', `kilau-${k}.png`), glowSvg(256, PICKUP_GLOW[k]), 256);
}

// Panah petunjuk: jingga = ke gudang, biru = ke rumah.
out(join(PUB, 'Panah', 'panah-jingga.png'), arrowSvg('#F09A2C', '#C46A12'), 128);
out(join(PUB, 'Panah', 'panah-biru.png'), arrowSvg('#1DB4E8', '#0A8DC2'), 128);

// Zona berhenti: dasar, tepi penuh (disingkap shader sesuai progres), dan lencana centang saja
// (gerak "zona-pop" hanya menganimasikan ikon tengah; lencananya sama dengan padSvg({ done: true })).
const doneBadge = k => `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <circle cx="128" cy="133" r="46" fill="#000" opacity=".22"/>
  <circle cx="128" cy="128" r="46" fill="#3F8F3E" stroke="${PAD_KINDS[k].d}" stroke-width="7"/>
  <g transform="translate(128 128)">${ICONS.centang()}</g></svg>`;
for (const k of Object.keys(PAD_KINDS)) {
  out(join(PUB, 'Zona', `zona-${k}.png`), padSvg(k), 256);
  out(join(PUB, 'Zona', `zona-${k}-penuh.png`), padSvg(k, { progress: 1 }), 256);
  out(join(PUB, 'Zona', `zona-${k}-centang.png`), doneBadge(k), 256);
}
out(join(PUB, 'Zona', 'kapsul.png'), finishAreaSvg(), 520);
out(join(PUB, 'Zona', 'kapsul-penuh.png'), finishAreaSvg({ progress: 1 }), 520);

console.log(`Sprite dunia: ${n} PNG`);
