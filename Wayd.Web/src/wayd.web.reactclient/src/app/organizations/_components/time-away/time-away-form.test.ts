import dayjs from 'dayjs'
import { TimeAwayReason } from '@/src/services/wayd-api'
import { toFormErrors } from '@/src/utils'
import { fieldForError, timeAwayRequestFor } from './time-away-form'

jest.unmock('dayjs')

describe('timeAwayRequestFor', () => {
  it('sends the days as calendar dates with the share and the reason', () => {
    const request = timeAwayRequestFor(
      {
        dates: [dayjs('2027-02-01'), dayjs('2027-02-05')],
        duration: 'half',
        reason: TimeAwayReason.Training,
      },
      8,
    )

    expect(request).toEqual({
      start: '2027-02-01',
      end: '2027-02-05',
      percentAway: 50,
      reason: 'Training',
    })
  })

  it('sends hours away as the share of the working day they are', () => {
    const request = timeAwayRequestFor(
      {
        dates: [dayjs('2027-02-01'), dayjs('2027-02-01')],
        duration: 'hours',
        hours: 2,
        reason: TimeAwayReason.Other,
      },
      8,
    )

    expect(request.percentAway).toBe(25)
  })

  it('keeps the day picked late in the evening, whatever the time zone', () => {
    const request = timeAwayRequestFor(
      {
        dates: [dayjs('2027-02-01T23:30:00'), dayjs('2027-02-01T23:30:00')],
        duration: 'full',
        reason: TimeAwayReason.PaidTimeOff,
      },
      8,
    )

    expect(request.start).toBe('2027-02-01')
    expect(request.end).toBe('2027-02-01')
  })
})

describe('fieldForError', () => {
  it('shows a date error on the date range', () => {
    // Arrange
    const errors = { End: ['Time away cannot end before it starts.'] }

    // Act
    const fields = toFormErrors(errors, { fieldFor: fieldForError('full') })

    // Assert
    expect(fields).toEqual([
      { name: 'dates', errors: ['Time away cannot end before it starts.'] },
    ])
  })

  it('shows a share error on the hours when they were entered', () => {
    // Arrange / Act / Assert
    expect(fieldForError('hours')('percentAway')).toBe('hours')
  })

  it('shows a share error on the duration otherwise', () => {
    // Arrange / Act / Assert
    expect(fieldForError('half')('percentAway')).toBe('duration')
  })
})
