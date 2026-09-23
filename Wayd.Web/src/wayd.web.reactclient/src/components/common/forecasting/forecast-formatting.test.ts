import { chanceOfFinishingBy } from './forecast-formatting'

jest.unmock('dayjs')

describe('chanceOfFinishingBy', () => {
  const histogram = [
    { date: '2026-10-01', trials: 30 },
    { date: '2026-10-03', trials: 50 },
  ]

  it('counts trials finishing on or before the date', () => {
    expect(chanceOfFinishingBy(histogram, 100, '2026-10-01')).toBe(0.3)
    expect(chanceOfFinishingBy(histogram, 100, '2026-10-02')).toBe(0.3)
    expect(chanceOfFinishingBy(histogram, 100, '2026-10-03')).toBe(0.8)
  })

  it('counts trials beyond the horizon against the chance', () => {
    // 20 of the 100 trials are not in the histogram: they never finished.
    expect(chanceOfFinishingBy(histogram, 100, '2030-01-01')).toBe(0.8)
  })

  it('is zero before the first finish and without trials', () => {
    expect(chanceOfFinishingBy(histogram, 100, '2026-09-30')).toBe(0)
    expect(chanceOfFinishingBy([], 0, '2026-10-01')).toBe(0)
  })
})
