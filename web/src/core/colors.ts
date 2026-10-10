import type { Rgb, Lab } from './types';

export function toLab(c: Rgb): Lab {
  const linear = (n: number) => { const v = n / 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; };
  const f = (t: number) => t > 216 / 24389 ? Math.cbrt(t) : (24389 / 27 * t + 16) / 116;
  const r = linear(c.R), g = linear(c.G), b = linear(c.B);
  const x = f((r * .4124564 + g * .3575761 + b * .1804375) / .95047);
  const y = f(r * .2126729 + g * .7151522 + b * .0721750);
  const z = f((r * .0193339 + g * .1191920 + b * .9503041) / 1.08883);
  return { L: 116 * y - 16, A: 500 * (x - y), B: 200 * (y - z) };
}
export function distance(p: Lab, q: Lab): number {
  const rad = Math.PI / 180;
  let c1 = Math.sqrt(p.A * p.A + p.B * p.B), c2 = Math.sqrt(q.A * q.A + q.B * q.B);
  let cb = (c1 + c2) / 2;
  const pow = cb ** 7, g = .5 * (1 - Math.sqrt(pow / (pow + 25 ** 7)));
  const a1 = (1 + g) * p.A, a2 = (1 + g) * q.A;
  c1 = Math.sqrt(a1 * a1 + p.B * p.B); c2 = Math.sqrt(a2 * a2 + q.B * q.B);
  const hue = (a: number, b: number) => { const h = Math.atan2(b, a) * 180 / Math.PI; return h < 0 ? h + 360 : h; };
  const h1 = hue(a1, p.B), h2 = hue(a2, q.B), dl = q.L - p.L, dc = c2 - c1;
  let dh = h2 - h1;
  if (c1 * c2 === 0) dh = 0; else if (dh > 180) dh -= 360; else if (dh < -180) dh += 360;
  const dH = 2 * Math.sqrt(c1 * c2) * Math.sin(dh / 2 * rad), lb = (p.L + q.L) / 2;
  cb = (c1 + c2) / 2;
  const hb = c1 * c2 === 0 ? h1 + h2 : Math.abs(h1 - h2) <= 180 ? (h1 + h2) / 2 : h1 + h2 < 360 ? (h1 + h2 + 360) / 2 : (h1 + h2 - 360) / 2;
  const t = 1 - .17 * Math.cos((hb - 30) * rad) + .24 * Math.cos(2 * hb * rad) + .32 * Math.cos((3 * hb + 6) * rad) - .20 * Math.cos((4 * hb - 63) * rad);
  const sl = 1 + .015 * (lb - 50) ** 2 / Math.sqrt(20 + (lb - 50) ** 2), sc = 1 + .045 * cb, sh = 1 + .015 * cb * t;
  const rc = 2 * Math.sqrt(cb ** 7 / (cb ** 7 + 25 ** 7));
  const rt = -rc * Math.sin(60 * Math.exp(-(((hb - 275) / 25) ** 2)) * rad);
  return Math.sqrt(Math.max(0, (dl / sl) ** 2 + (dc / sc) ** 2 + (dH / sh) ** 2 + rt * (dc / sc) * (dH / sh)));
}
