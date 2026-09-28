// Lint: CSS komponen desain hanya boleh memakai subset yang bisa diterjemahkan ke USS (Unity 6.2).
// Pemakaian: node Tools/art/lint-uss-css.mjs <file.css> [...]
// Daftar ini sengaja konservatif; dicocokkan ulang dengan referensi USS di langkah U0.
import { readFileSync } from 'node:fs';

const SIDES = ['', '-top', '-right', '-bottom', '-left'];
const ALLOWED = new Set([
  'align-content', 'align-items', 'align-self', 'justify-content',
  'flex', 'flex-basis', 'flex-direction', 'flex-grow', 'flex-shrink', 'flex-wrap', 'display',
  'position', 'top', 'right', 'bottom', 'left',
  'width', 'height', 'min-width', 'min-height', 'max-width', 'max-height',
  ...SIDES.map(s => 'margin' + s), ...SIDES.map(s => 'padding' + s),
  ...SIDES.map(s => `border${s}-width`), ...SIDES.map(s => `border${s}-color`),
  'border-radius', 'border-top-left-radius', 'border-top-right-radius',
  'border-bottom-left-radius', 'border-bottom-right-radius',
  'background-color', 'background-image', 'background-size', 'background-position', 'background-repeat',
  'color', 'font-size', 'letter-spacing', 'word-spacing', 'white-space', 'text-overflow', 'text-shadow',
  'opacity', 'overflow', 'visibility', 'cursor',
  'rotate', 'scale', 'translate', 'transform-origin',
  'transition', 'transition-property', 'transition-duration', 'transition-delay', 'transition-timing-function',
  // Diterjemahkan saat konversi: font-family/font-weight → -unity-font-definition + -unity-font-style,
  // text-align → -unity-text-align.
  'font-family', 'font-weight', 'text-align',
]);
const BAD_VALUE = [
  [/\b\d*\.?\d+(em|rem|vh|vw|vmin|vmax|ch|ex|fr)\b/, 'satuan tidak ada di USS (pakai px atau %)'],
  [/\bcalc\(/, 'calc() tidak ada di USS'],
  [/gradient\(/, 'gradien tidak ada di USS (pakai tekstur)'],
  [/\bhsla?\(/, 'hsl() tidak ada di USS (pakai hex/rgb)'],
  [/\binherit\b/, 'inherit tidak didukung; biarkan properti yang memang diwariskan'],
  [/!important/, '!important tidak ada di USS'],
];
const BAD_SELECTOR = [
  [/::?(before|after|first-line|first-letter|placeholder)/, 'pseudo-element tidak ada di USS'],
  [/:(nth-|first-child|last-child|not\(|has\(|is\(|where\()/, 'pseudo-class struktural tidak ada di USS'],
  [/\[[^\]]*\]/, 'selector atribut tidak ada di USS'],
  [/[+~](?![^(]*\))/, 'kombinator saudara (+ ~) tidak ada di USS'],
];
const ALLOWED_PSEUDO = /:(hover|active|focus|disabled|enabled|checked|root)\b/g;

let problems = 0;
for (const file of process.argv.slice(2)) {
  const src = readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, ' '));
  const lineOf = i => src.slice(0, i).split('\n').length;
  const report = (i, msg) => { problems++; console.log(`${file}:${lineOf(i)}: ${msg}`); };
  if (/@(media|keyframes|font-face|supports|layer|container)/.test(src)) {
    const m = src.match(/@(media|keyframes|font-face|supports|layer|container)/);
    report(m.index, `@${m[1]} tidak ada di USS`);
  }
  const rule = /([^{}]+)\{([^{}]*)\}/g;
  let r;
  while ((r = rule.exec(src))) {
    const selector = r[1].trim();
    const selStart = r.index + r[0].indexOf(r[1].trim());
    for (const [re, msg] of BAD_SELECTOR) if (re.test(selector)) report(selStart, `"${selector}": ${msg}`);
    const leftover = selector.replace(ALLOWED_PSEUDO, '').match(/::?[a-z-]+/);
    if (leftover && !BAD_SELECTOR.some(([re]) => re.test(selector)))
      report(selStart, `"${selector}": pseudo-class ${leftover[0]} tidak dikenal USS`);
    const bodyStart = r.index + r[0].indexOf('{') + 1;
    const decl = /([-\w]+)\s*:\s*([^;]+);?/g;
    let d;
    while ((d = decl.exec(r[2]))) {
      const [, prop, value] = d;
      const at = bodyStart + d.index;
      if (prop.startsWith('--')) continue;
      if (!ALLOWED.has(prop)) report(at, `properti "${prop}" tidak ada di USS`);
      for (const [re, msg] of BAD_VALUE) if (re.test(value)) report(at, `${prop}: ${msg}`);
      if (prop === 'display' && !/^(flex|none)$/.test(value.trim())) report(at, `display: ${value.trim()} (USS hanya flex/none)`);
      if (prop === 'position' && !/^(absolute|relative)$/.test(value.trim())) report(at, `position: ${value.trim()} (USS hanya absolute/relative)`);
      if (prop === 'font-weight' && !/^(400|600|700|var\(.+\))$/.test(value.trim())) report(at, `font-weight ${value.trim()} tidak punya aset font`);
    }
  }
}
if (problems) { console.log(`\n${problems} masalah. CSS ini belum bisa dipindah ke USS apa adanya.`); process.exit(1); }
console.log('Lint USS: bersih.');
