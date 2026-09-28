// Baca/tulis PNG RGBA 8-bit tanpa dependensi (zlib bawaan Node).
// Cukup untuk aset proyek ini: truecolor/truecolor+alfa/palet, non-interlace.
import { inflateSync, deflateSync } from 'node:zlib';
import { readFileSync, writeFileSync } from 'node:fs';

const SIG = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

export function readPng(path) {
  const buf = readFileSync(path);
  if (!buf.subarray(0, 8).equals(SIG)) throw new Error(`${path}: bukan PNG`);
  let pos = 8, width = 0, height = 0, depth = 0, type = 0, interlace = 0, palette = null, trns = null;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos), kind = buf.toString('ascii', pos + 4, pos + 8), data = buf.subarray(pos + 8, pos + 8 + len);
    if (kind === 'IHDR') { width = data.readUInt32BE(0); height = data.readUInt32BE(4); depth = data[8]; type = data[9]; interlace = data[12]; }
    else if (kind === 'PLTE') palette = data;
    else if (kind === 'tRNS') trns = data;
    else if (kind === 'IDAT') idat.push(data);
    else if (kind === 'IEND') break;
    pos += 12 + len;
  }
  if (depth !== 8 || interlace) throw new Error(`${path}: hanya PNG 8-bit non-interlace (depth ${depth}, interlace ${interlace})`);
  const channels = { 2: 3, 6: 4, 3: 1, 0: 1, 4: 2 }[type];
  if (!channels) throw new Error(`${path}: tipe warna ${type} tidak didukung`);
  const raw = inflateSync(Buffer.concat(idat)), stride = width * channels, px = Buffer.alloc(height * stride);
  for (let y = 0; y < height; y++) {
    const f = raw[y * (stride + 1)], src = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const row = px.subarray(y * stride, (y + 1) * stride), prev = y ? px.subarray((y - 1) * stride, y * stride) : null;
    for (let i = 0; i < stride; i++) {
      const a = i >= channels ? row[i - channels] : 0, b = prev ? prev[i] : 0, c = prev && i >= channels ? prev[i - channels] : 0;
      let v = src[i];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      row[i] = v & 255;
    }
  }
  const rgba = new Uint8ClampedArray(width * height * 4);
  for (let i = 0, j = 0; i < width * height; i++, j += 4) {
    if (type === 6) { rgba[j] = px[i * 4]; rgba[j + 1] = px[i * 4 + 1]; rgba[j + 2] = px[i * 4 + 2]; rgba[j + 3] = px[i * 4 + 3]; }
    else if (type === 2) { rgba[j] = px[i * 3]; rgba[j + 1] = px[i * 3 + 1]; rgba[j + 2] = px[i * 3 + 2]; rgba[j + 3] = 255; }
    else if (type === 3) { const k = px[i]; rgba[j] = palette[k * 3]; rgba[j + 1] = palette[k * 3 + 1]; rgba[j + 2] = palette[k * 3 + 2]; rgba[j + 3] = trns && k < trns.length ? trns[k] : 255; }
    else if (type === 0) { rgba[j] = rgba[j + 1] = rgba[j + 2] = px[i]; rgba[j + 3] = 255; }
    else { rgba[j] = rgba[j + 1] = rgba[j + 2] = px[i * 2]; rgba[j + 3] = px[i * 2 + 1]; }
  }
  return { width, height, data: rgba };
}

const CRC = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c; });
const crc32 = b => { let c = -1; for (const x of b) c = CRC[(c ^ x) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
const chunk = (kind, data) => {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0); out.write(kind, 4, 'ascii'); data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
};

export function encodePng({ width, height, data }) {
  const raw = Buffer.alloc(height * (width * 4 + 1));
  for (let y = 0; y < height; y++) {
    raw[y * (width * 4 + 1)] = 0;
    Buffer.from(data.buffer, data.byteOffset + y * width * 4, width * 4).copy(raw, y * (width * 4 + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([SIG, chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

export const writePng = (path, img) => writeFileSync(path, encodePng(img));

export function rgbToHsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  let h = 0;
  if (d) h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return [((h / 6) + 1) % 1, max ? d / max : 0, max];
}
