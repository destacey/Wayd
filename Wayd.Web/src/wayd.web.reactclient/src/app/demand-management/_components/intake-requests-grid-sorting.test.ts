import {
  IntakeRequestSummaryDto,
  NeedByBasis,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'
import { compareNeedBy } from './intake-requests-grid'

const withNeedBy = (
  id: string,
  date?: string,
  family = NeedByBasisFamily.ChosenByUs,
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
      withNeedBy('later', '2027-03-31'),
      withNeedBy('chosen', '2026-12-31'),
      withNeedBy('fixed', '2026-12-31', NeedByBasisFamily.FixedForUs),
    ]

    // Act
    const ordered = [...requests].sort(compareNeedBy).map((r) => r.id)

    // Assert
    expect(ordered).toEqual(['fixed', 'chosen', 'later', 'none'])
  })
})
