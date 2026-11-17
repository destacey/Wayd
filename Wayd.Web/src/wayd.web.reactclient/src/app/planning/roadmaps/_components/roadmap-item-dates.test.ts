import { itemDatesRequest } from './roadmap-item-dates'
import { asCalendarDate } from '@/src/utils/calendar-date'

const ids = { roadmapId: 'roadmap-1', itemId: 'item-1' }

describe('itemDatesRequest', () => {
  it('moves a milestone by its date alone', () => {
    // Act
    const request = itemDatesRequest({
      ...ids,
      $type: 'milestone',
      start: asCalendarDate('2026-05-01'),
      end: asCalendarDate('2026-05-01'),
    })

    // Assert — a range would leave the milestone without a date
    expect(request).toEqual({ ...ids, $type: 'milestone', date: '2026-05-01' })
  })

  it.each(['activity', 'timebox'])('moves a %s by its range', ($type) => {
    // Act
    const request = itemDatesRequest({
      ...ids,
      $type,
      start: asCalendarDate('2026-05-01'),
      end: asCalendarDate('2026-05-29'),
    })

    // Assert
    expect(request).toEqual({
      ...ids,
      $type,
      start: '2026-05-01',
      end: '2026-05-29',
    })
  })
})
