import {
  durationOf,
  formatHours,
  formatQuarterHours,
  hoursFor,
  percentAwayFor,
  percentForHours,
  timeAwayLabel,
} from './time-away-duration'

describe('percentAwayFor', () => {
  it.each([
    ['full', undefined, 8, 100],
    ['half', undefined, 8, 50],
    ['hours', 2, 8, 25],
    ['hours', 1.5, 8, 19],
    ['hours', 3, 7.5, 40],
    ['hours', 12, 8, 100],
    ['hours', 0, 8, 1],
  ] as const)(
    'records %s with %p hours of a %p-hour day as %p%%',
    (duration, hours, workingDayHours, expected) => {
      expect(percentAwayFor(duration, hours, workingDayHours)).toBe(expected)
    },
  )
})

describe('durationOf', () => {
  it.each([
    [100, 8, { duration: 'full' }],
    [50, 8, { duration: 'half' }],
    [25, 8, { duration: 'hours', hours: 2 }],
    [19, 8, { duration: 'hours', hours: 1.5 }],
    [40, 7.5, { duration: 'hours', hours: 3 }],
  ])('reads %p%% of a %p-hour day back as %o', (percent, hours, expected) => {
    expect(durationOf(percent, hours)).toEqual(expected)
  })
})

describe('timeAwayLabel', () => {
  it.each([
    [100, 8, 'Full day'],
    [50, 8, 'Half day'],
    [25, 8, '2 h'],
    [75, 8, '6 h'],
    [19, 8, '1.5 h'],
  ])('reads %p%% of a %p-hour day as %p', (percent, hours, expected) => {
    expect(timeAwayLabel(percent, hours)).toBe(expected)
  })
})

describe('hours and percents', () => {
  it('converts a share of the day to hours and back', () => {
    expect(hoursFor(70, 8)).toBeCloseTo(5.6)
    expect(percentForHours(5.6, 8)).toBe(70)
    expect(percentForHours(9, 8)).toBe(100)
    expect(percentForHours(-1, 8)).toBe(0)
  })

  it('shows hours to two decimals without trailing zeros', () => {
    expect(formatHours(5.6)).toBe('5.6')
    expect(formatHours(4)).toBe('4')
    expect(formatHours(2.64)).toBe('2.64')
    expect(formatHours(1.005)).toBe('1')
  })

  it('shows time away to the nearest quarter hour', () => {
    expect(formatQuarterHours(1.52)).toBe('1.5')
    expect(formatQuarterHours(2.26)).toBe('2.25')
  })
})
