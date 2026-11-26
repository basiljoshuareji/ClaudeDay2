import { daysBetween, dueLabel, isOverdue, toIsoDate, todayIso } from './dates';

describe('date utils', () => {
  it('formats a local date as yyyy-MM-dd with zero padding', () => {
    expect(toIsoDate(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05');
    expect(todayIso(new Date(2026, 10, 26, 8, 0))).toBe('2026-11-26');
  });

  it('counts whole days between two dates, including across month and DST boundaries', () => {
    expect(daysBetween('2026-11-26', '2026-11-26')).toBe(0);
    expect(daysBetween('2026-11-26', '2026-11-27')).toBe(1);
    expect(daysBetween('2026-12-01', '2026-11-30')).toBe(-1);
    expect(daysBetween('2026-03-07', '2026-03-09')).toBe(2);
  });

  describe('isOverdue', () => {
    it('is true only for past, unfinished tasks', () => {
      expect(isOverdue('2026-11-25', 'Pending', '2026-11-26')).toBe(true);
      expect(isOverdue('2026-11-25', 'InProgress', '2026-11-26')).toBe(true);
    });

    it('is false for completed, due-today, future and undated tasks', () => {
      expect(isOverdue('2026-11-25', 'Completed', '2026-11-26')).toBe(false);
      expect(isOverdue('2026-11-26', 'Pending', '2026-11-26')).toBe(false);
      expect(isOverdue('2026-11-27', 'Pending', '2026-11-26')).toBe(false);
      expect(isOverdue(null, 'Pending', '2026-11-26')).toBe(false);
    });
  });

  describe('dueLabel', () => {
    const today = '2026-11-26';

    it('uses friendly words near today', () => {
      expect(dueLabel('2026-11-26', today)).toBe('Today');
      expect(dueLabel('2026-11-27', today)).toBe('Tomorrow');
      expect(dueLabel('2026-11-25', today)).toBe('Yesterday');
    });

    it('falls back to a short date, adding the year only when it differs', () => {
      expect(dueLabel('2026-12-24', today)).toMatch(/24/);
      expect(dueLabel('2026-12-24', today)).not.toMatch(/2026/);
      expect(dueLabel('2027-01-15', today)).toMatch(/2027/);
    });
  });
});
