/** A local calendar day as the API writes dates (`yyyy-MM-dd`), `offset` days from today. */
export function localDateIso(offset = 0, now = new Date()): string {
  const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() + offset);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}`;
}

/** Today or yesterday for a `yyyy-MM-dd` date, so it can be said in words; null for any other day. */
export function nearDayOf(date: string, now = new Date()): 'today' | 'yesterday' | null {
  if (date === localDateIso(0, now)) {
    return 'today';
  }
  return date === localDateIso(-1, now) ? 'yesterday' : null;
}

/** How recipe screens write a day, for `DatePipe`. */
export const RECIPE_DATE_FORMAT = 'd MMM y';