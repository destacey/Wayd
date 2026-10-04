// The limits compare real days and times; the global mock only stubs format.
jest.unmock('dayjs')

import dayjs from 'dayjs'
import { disabledTimeAfter } from './past-moment'

describe('disabledTimeAfter', () => {
  const latest = dayjs('2026-10-04T14:30:00')

  it('disables the later hours and minutes on the day of the limit', () => {
    // Act
    const result = disabledTimeAfter(latest)(dayjs('2026-10-04T09:00:00'))

    // Assert
    expect(result.disabledHours?.()).toEqual([
      15, 16, 17, 18, 19, 20, 21, 22, 23,
    ])
    expect(result.disabledMinutes?.(14)).toEqual(
      Array.from({ length: 29 }, (_, i) => 31 + i),
    )
    expect(result.disabledMinutes?.(13)).toEqual([])
  })

  it('disables nothing on an earlier day', () => {
    // Act
    const result = disabledTimeAfter(latest)(dayjs('2026-10-03T23:00:00'))

    // Assert
    expect(result).toEqual({})
  })
})
