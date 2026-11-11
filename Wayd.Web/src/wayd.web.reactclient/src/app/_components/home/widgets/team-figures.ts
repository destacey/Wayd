const DAY_MS = 24 * 60 * 60 * 1000

/** A count with its noun, singular for one: "1 team", "3 teams". */
export const plural = (count: number, one: string, many = `${one}s`) =>
  `${count} ${count === 1 ? one : many}`

/** Whole days since `since`, at least zero. */
export const daysSince = (since: Date, now: Date = new Date()): number =>
  Math.max(0, Math.floor((now.getTime() - new Date(since).getTime()) / DAY_MS))
