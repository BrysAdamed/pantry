import { localDateIso, nearDayOf } from './local-date';

describe('local dates', () => {
  const now = new Date(2026, 0, 1, 0, 30);

  it('writes the local day as the API does, across a year boundary', () => {
    expect(localDateIso(0, now)).toBe('2026-01-01');
    expect(localDateIso(-1, now)).toBe('2025-12-31');
  });

  it('names today and yesterday only', () => {
    expect(nearDayOf('2026-01-01', now)).toBe('today');
    expect(nearDayOf('2025-12-31', now)).toBe('yesterday');
    expect(nearDayOf('2025-12-30', now)).toBeNull();
  });
});
