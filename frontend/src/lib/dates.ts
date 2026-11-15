/** Plan days are calendar dates ("2030-01-07"), never timestamps. All maths here is on UTC midnight to avoid DST shifts. */

export function parseDate(iso: string): Date {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d));
}

export function toIso(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function addDays(iso: string, days: number): string {
  const d = parseDate(iso);
  d.setUTCDate(d.getUTCDate() + days);
  return toIso(d);
}

export function mondayOf(iso: string): string {
  const day = parseDate(iso).getUTCDay(); // 0 = Sunday
  return addDays(iso, -((day + 6) % 7));
}

/** Today's date in the household's time zone. */
export function todayIn(timeZone: string): string {
  try {
    return new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
  } catch {
    return toIso(new Date());
  }
}

export function weekDays(weekStart: string): string[] {
  return Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
}

const fmt = (opts: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat("en-AU", { timeZone: "UTC", ...opts });

export function formatWeek(weekStart: string): string {
  const start = parseDate(weekStart);
  const end = parseDate(addDays(weekStart, 6));
  const sameMonth = start.getUTCMonth() === end.getUTCMonth();
  return sameMonth
    ? `${start.getUTCDate()}–${fmt({ day: "numeric", month: "short" }).format(end)}`
    : `${fmt({ day: "numeric", month: "short" }).format(start)} – ${fmt({ day: "numeric", month: "short" }).format(end)}`;
}

export function dayName(iso: string, style: "short" | "long" = "short"): string {
  return fmt({ weekday: style }).format(parseDate(iso));
}

export function dayOfMonth(iso: string): number {
  return parseDate(iso).getUTCDate();
}
