// Render SVG ke PNG dengan resvg (alat dev saja, tidak ikut ke build game).
import { Resvg } from '@resvg/resvg-js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

export function svgToPng(svg, width) {
  const opts = width ? { fitTo: { mode: 'width', value: width } } : {};
  return new Resvg(svg, { ...opts, font: { loadSystemFonts: false } }).render().asPng();
}

export function writeSvgPng(path, svg, width) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, svgToPng(svg, width));
}

// Isi <svg> tanpa pembungkusnya, untuk disusun di lembar kontak.
export const svgInner = svg => svg.replace(/^<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
