import dayjs from 'dayjs'
import { TimeAwayReason } from '@/src/services/wayd-api'
import { timeAwayRequestFor } from './time-away-form'

jest.unmock('dayjs')

describe('timeAwayRequestFor', () => {
  it('sends the days as calendar dates with the share and the reason', () => {
    const request = timeAwayRequestFor({
      start: dayjs('2027-02-01'),
      end: dayjs('2027-02-05'),
      percentAway: 50,
      reason: TimeAwayReason.Training,
    })

    expect(request).toEqual({
      start: '2027-02-01',
      end: '2027-02-05',
      percentAway: 50,
      reason: 'Training',
    })
  })

  it('keeps the day picked late in the evening, whatever the time zone', () => {
    const request = timeAwayRequestFor({
      start: dayjs('2027-02-01T23:30:00'),
      end: dayjs('2027-02-01T23:30:00'),
      percentAway: 100,
      reason: TimeAwayReason.PaidTimeOff,
    })

    expect(request.start).toBe('2027-02-01')
    expect(request.end).toBe('2027-02-01')
  })
})
