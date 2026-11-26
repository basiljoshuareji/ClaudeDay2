// Verifies WCAG contrast for the theme palettes in src/styles/_themes.scss.
// Usage: node scripts/check-contrast.mjs
import { readFileSync } from 'node:fs';

const css = readFileSync(new URL('../src/styles/_themes.scss', import.meta.url), 'utf8');
const blocks = [...css.matchAll(/(?:^|\n)((?::root,\s*)?\[data-theme='(\w+)'\])\s*\{([^}]*)\}/g)];

const hex = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16) / 255);
const lin = (c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const lum = (h) => { const [r, g, b] = hex(h).map(lin); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

// [foreground, background, minimum ratio, description]
const pairs = [
  ['text', 'bg', 4.5], ['text', 'surface', 4.5], ['text', 'surface-2', 4.5],
  ['text-muted', 'bg', 4.5], ['text-muted', 'surface', 4.5], ['text-muted', 'surface-2', 4.5],
  ['on-primary', 'primary', 4.5], ['on-primary', 'primary-hover', 4.5],
  ['primary', 'surface', 4.5], ['primary', 'bg', 4.5], ['primary', 'primary-soft', 4.5],
  ['primary-hover', 'primary-soft', 4.5],
  ['danger', 'surface', 4.5], ['danger', 'danger-soft', 4.5], ['danger', 'bg', 4.5],
  ['success', 'surface', 4.5], ['success', 'success-soft', 4.5],
  ['warning', 'surface', 4.5], ['warning', 'warning-soft', 4.5],
  ['prio-high', 'surface', 4.5], ['prio-medium', 'surface', 4.5], ['prio-low', 'surface', 4.5],
  ['accent', 'surface', 4.5],
  ['border-strong', 'surface', 3], ['border-strong', 'bg', 3],
  ['primary', 'surface', 3],
];

let failures = 0;
for (const [, , name, body] of blocks) {
  const vars = Object.fromEntries([...body.matchAll(/--([\w-]+):\s*(#[0-9a-fA-F]{6})/g)].map((m) => [m[1], m[2]]));
  for (const [fg, bg, min] of pairs) {
    const r = ratio(vars[fg], vars[bg]);
    if (r < min) { failures++; console.log(`FAIL ${name}: ${fg} on ${bg} = ${r.toFixed(2)} (needs ${min})`); }
  }
  console.log(`checked ${name}`);
}
console.log(failures ? `\n${failures} failing pair(s)` : '\nAll pairs pass WCAG AA');
process.exit(failures ? 1 : 0);
