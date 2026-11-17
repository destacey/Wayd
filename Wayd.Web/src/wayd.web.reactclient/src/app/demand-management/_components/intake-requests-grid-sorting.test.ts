import {
  IntakeRequestSummaryDto,
  NeedByBasis,
  NeedByBasisFamily,
} from '@/src/services/api/wayd.schemas'
import { compareNeedBy } from './intake-requests-grid'
import { asCalendarDate } from '@/src/utils/calendar-date'

const withNeedBy = (
  id: string,
  date?: string,
  family: NeedByBasisFamily = NeedByBasisFamily.ChosenByUs,
): IntakeRequestSummaryDto =>
  ({
    id,
    needBy: date
      ? {
          date,
          basis:
            family === NeedByBasisFamily.FixedForUs
              ? NeedByBasis.Regulatory
              : NeedByBasis.Commitment,
          family,
          reference: '',
        }
      : undefined,
  }) as IntakeRequestSummaryDto

describe('compareNeedBy', () => {
  it('orders by date, a fixed date first on the same day, and no date last', () => {
    // Arrange
    const requests = [
      withNeedBy('none'),
      withNeedBy('later', asCalendarDate('2027-03-31')),
      withNeedBy('chosen', asCalendarDate('2026-12-31')),
      withNeedBy(
        'fixed',
        asCalendarDate('2026-12-31'),
        NeedByBasisFamily.FixedForUs,
      ),
    ]

    // Act
    const ordered = [...requests].sort(compareNeedBy).map((r) => r.id)

    // Assert
    expect(ordered).toEqual(['fixed', 'chosen', 'later', 'none'])
  })
})
