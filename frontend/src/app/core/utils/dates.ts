import { TaskStatus } from '../models/api.models';

const DAY_MS = 86_400_000;

/** Formats a Date as yyyy-MM-dd using the *local* calendar date. */
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function todayIso(now: Date = new Date()): string {
  return toIsoDate(now);
}

/** Whole days from `from` to `to` (both yyyy-MM-dd). Positive when `to` is later. */
export function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS);
}

export function isOverdue(dueDate: string | null, status: TaskStatus, today: string = todayIso()): boolean {
  return !!dueDate && status !== 'Completed' && dueDate < today;
}

/** Friendly relative label such as "Today", "Tomorrow", "Yesterday" or "Mon, 12 Jan". */
export function dueLabel(dueDate: string, today: string = todayIso()): string {
  const diff = daysBetween(today, dueDate);
  if (diff === 0) return 'Today';
  if (diff === 1) return 'Tomorrow';
  if (diff === -1) return 'Yesterday';

  const sameYear = dueDate.slice(0, 4) === today.slice(0, 4);
  return new Intl.DateTimeFormat(undefined, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: sameYear ? undefined : 'numeric',
    timeZone: 'UTC',
  }).format(new Date(`${dueDate}T00:00:00Z`));
}
