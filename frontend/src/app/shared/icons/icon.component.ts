import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** A path string, a circle [cx, cy, r] (radius < 2 renders filled), or a rect {x,y,w,h,rx}. */
type Shape = string | readonly [number, number, number] | { x: number; y: number; w: number; h: number; rx: number };

/** Minimal inline icon set (24x24, stroke based) so the app has no icon-font or network dependency. */
const ICONS: Record<string, readonly Shape[]> = {
  home: ['M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z'],
  briefcase: [{ x: 2, y: 7, w: 20, h: 14, rx: 2 }, 'M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2', 'M2 13h20'],
  folder: ['M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'],
  star: ['m12 3 2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z'],
  heart: ['M12 20s-7-4.6-9-9.2C1.8 7.4 4 4 7.2 4c1.9 0 3.4 1 4.8 2.8C13.4 5 14.9 4 16.8 4 20 4 22.2 7.4 21 10.8 19 15.4 12 20 12 20z'],
  book: ['M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z', 'M4 19a2 2 0 0 1 2-2h13'],
  cart: [[9, 20, 1.5], [18, 20, 1.5], 'M2 3h3l2.7 12.4a1 1 0 0 0 1 .8h8.9a1 1 0 0 0 1-.8L20 7H6'],
  calendar: [{ x: 3, y: 5, w: 18, h: 16, rx: 2 }, 'M16 3v4M8 3v4M3 11h18'],
  dumbbell: ['M6.5 6.5v11M17.5 6.5v11M3 9v6M21 9v6M6.5 12h11'],
  music: ['M9 18V5l12-2v13', [6, 18, 3], [18, 16, 3]],
  code: ['m8 8-5 4 5 4M16 8l5 4-5 4M14 4l-4 16'],
  coffee: ['M4 8h13v6a5 5 0 0 1-5 5H9a5 5 0 0 1-5-5z', 'M17 10h2a2 2 0 0 1 0 4h-2M8 3v2M12 3v2'],
  leaf: ['M5 19C5 10 10 4 20 4c0 10-6 16-15 15z', 'M5 19c3-5 6-8 10-10'],
  target: [[12, 12, 9], [12, 12, 5], [12, 12, 1]],
  bolt: ['M13 3 5 14h6l-1 7 8-11h-6z'],
  gift: [{ x: 3, y: 8, w: 18, h: 4, rx: 1 }, 'M12 8v13M5 12v9h14v-9', 'M12 8c-2-4-6-3-5 0 .6 1.5 5 0 5 0zM12 8c2-4 6-3 5 0-.6 1.5-5 0-5 0z'],
  check: ['m5 12 5 5 9-10'],
  plus: ['M12 5v14M5 12h14'],
  trash: ['M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6'],
  edit: ['M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z', 'M13.5 6.5l4 4'],
  search: [[11, 11, 7], 'm20 20-3.5-3.5'],
  menu: ['M4 6h16M4 12h16M4 18h16'],
  x: ['M6 6l12 12M18 6 6 18'],
  'chevron-down': ['m6 9 6 6 6-6'],
  'chevron-right': ['m9 6 6 6-6 6'],
  'chevron-left': ['m15 6-6 6 6 6'],
  'chevrons-left': ['m11 17-5-5 5-5M18 17l-5-5 5-5'],
  'chevrons-right': ['m6 17 5-5-5-5M13 17l5-5-5-5'],
  grip: [[9, 6, 1.3], [15, 6, 1.3], [9, 12, 1.3], [15, 12, 1.3], [9, 18, 1.3], [15, 18, 1.3]],
  more: [[12, 5, 1.4], [12, 12, 1.4], [12, 19, 1.4]],
  settings: ['M4 6h8M16 6h4M4 12h2M10 12h10M4 18h10M18 18h2', [14, 6, 2], [8, 12, 2], [16, 18, 2]],
  logout: ['M9 4H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4M16 8l4 4-4 4M20 12H9'],
  palette: ['M12 3a9 9 0 1 0 0 18c1.2 0 2-.8 2-1.8 0-.5-.2-.9-.5-1.3-.3-.4-.5-.8-.5-1.3 0-1 .8-1.8 1.8-1.8H17a4 4 0 0 0 4-4c0-4-4-7.8-9-7.8z', [7.5, 11, 1.2], [10, 7, 1.2], [15, 7.5, 1.2]],
  sun: [[12, 12, 4], 'M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4'],
  moon: ['M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z'],
  layers: ['m12 3 9 5-9 5-9-5z', 'm3 12 9 5 9-5', 'm3 16 9 5 9-5'],
  flag: ['M5 21V4M5 4h11l-2 4 2 4H5'],
  tag: ['M3 12V4h8l10 10-8 8z', [7.5, 8.5, 1.3]],
  'list-check': ['M10 6h11M10 12h11M10 18h11M3 6l1.5 1.5L7 5M3 12l1.5 1.5L7 11M3 18l1.5 1.5L7 17'],
  alert: ['M12 3 2 20h20z', 'M12 10v4M12 17h.01'],
  clock: [[12, 12, 9], 'M12 7v5l3 2'],
  undo: ['M9 14 4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3'],
  filter: ['M3 5h18l-7 8v6l-4-2v-4z'],
  'arrow-up': ['M12 19V5M6 11l6-6 6 6'],
  'arrow-down': ['M12 5v14M6 13l6 6 6-6'],
  user: [[12, 8, 4], 'M4 21a8 8 0 0 1 16 0'],
  move: ['M4 8h14l-4-4M20 16H6l4 4'],
  inbox: ['M3 13l3-8h12l3 8v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z', 'M3 13h5l1 3h6l1-3h5'],
  sparkles: ['M12 3l1.8 4.7L18.5 9.5l-4.7 1.8L12 16l-1.8-4.7L5.5 9.5l4.7-1.8z', 'M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z'],
  info: [[12, 12, 9], 'M12 11v6M12 7.5h.01'],
  'check-circle': [[12, 12, 9], 'm8 12 3 3 5-6'],
  eye: ['M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z', [12, 12, 3]],
  'eye-off': ['M3 3l18 18', 'M10.6 6.1A9.8 9.8 0 0 1 12 6c6 0 10 6 10 6a17 17 0 0 1-3.2 3.9M6.2 7.8A16 16 0 0 0 2 12s4 7 10 7a9.6 9.6 0 0 0 4-.9'],
  circle: [[12, 12, 9]],
};

export const ICON_NAMES = Object.keys(ICONS);

/** Icons offered when creating or editing a section. */
export const SECTION_ICON_NAMES: readonly string[] = [
  'home',
  'briefcase',
  'folder',
  'star',
  'heart',
  'book',
  'cart',
  'calendar',
  'dumbbell',
  'music',
  'code',
  'coffee',
  'leaf',
  'target',
  'bolt',
  'gift',
];

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      viewBox="0 0 24 24"
      [attr.width]="size()"
      [attr.height]="size()"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @for (shape of shapes(); track $index) {
        @if (path(shape); as d) {
          <path [attr.d]="d" />
        } @else if (circle(shape); as c) {
          <circle [attr.cx]="c[0]" [attr.cy]="c[1]" [attr.r]="c[2]" [attr.fill]="c[2] < 2 ? 'currentColor' : 'none'" />
        } @else if (rect(shape); as r) {
          <rect [attr.x]="r.x" [attr.y]="r.y" [attr.width]="r.w" [attr.height]="r.h" [attr.rx]="r.rx" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      line-height: 0;
    }
  `,
})
export class IconComponent {
  readonly name = input.required<string>();
  readonly size = input(18);

  protected readonly shapes = computed<readonly Shape[]>(() => ICONS[this.name()] ?? ICONS['folder']);

  protected path(shape: Shape): string | null {
    return typeof shape === 'string' ? shape : null;
  }

  protected circle(shape: Shape): readonly [number, number, number] | null {
    return Array.isArray(shape) ? (shape as readonly [number, number, number]) : null;
  }

  protected rect(shape: Shape): { x: number; y: number; w: number; h: number; rx: number } | null {
    return typeof shape === 'object' && !Array.isArray(shape) ? (shape as { x: number; y: number; w: number; h: number; rx: number }) : null;
  }
}
