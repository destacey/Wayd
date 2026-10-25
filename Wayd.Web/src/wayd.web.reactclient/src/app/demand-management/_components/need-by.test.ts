import dayjs from 'dayjs'
import {
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'
import { needByFieldFor, toNeedByRequest } from './need-by'

// jest.setup stubs dayjs down to formatting; turning a picked day into a calendar date needs the real one.
jest.unmock('dayjs')

const basis = (
  code: NeedByBasis,
  recordsCommitment: boolean,
): NeedByBasisDto => ({
  id: 1,
  code,
  name: code,
  order: 1,
  family: NeedByBasisFamily.ChosenByUs,
  recordsCommitment,
})

const BASES = [
  basis(NeedByBasis.Commitment, true),
  basis(NeedByBasis.RequesterPreference, false),
]

describe('toNeedByRequest', () => {
  it('sends no need-by when the date is cleared, whatever else the form still holds', () => {
    // Arrange
    const values = {
      date: null,
      basis: NeedByBasis.RequesterPreference,
      reference: 'Before the board',
    }

    // Act
    const result = toNeedByRequest(values, BASES)

    // Assert
    expect(result).toBeUndefined()
  })

  it('sends the picked day as a calendar date', () => {
    // Arrange
    const values = {
      date: dayjs('2026-12-31'),
      basis: NeedByBasis.RequesterPreference,
      reference: '  Before the board  ',
    }

    // Act
    const result = toNeedByRequest(values, BASES)

    // Assert
    expect(result).toEqual({
      date: '2026-12-31',
      basis: NeedByBasis.RequesterPreference,
      reference: 'Before the board',
      committedById: undefined,
      committedOn: undefined,
    })
  })

  it('drops commitment fields left over from a basis that no longer records one', () => {
    // Arrange
    const values = {
      date: dayjs('2026-12-31'),
      basis: NeedByBasis.RequesterPreference,
      reference: 'Before the board',
      committedById: 'e-1',
      committedOn: dayjs('2026-09-03'),
    }

    // Act
    const result = toNeedByRequest(values, BASES)

    // Assert
    expect(result?.committedById).toBeUndefined()
    expect(result?.committedOn).toBeUndefined()
  })

  it('sends who made a commitment and when for a basis that records one', () => {
    // Arrange
    const values = {
      date: dayjs('2026-12-31'),
      basis: NeedByBasis.Commitment,
      reference: 'Steering committee',
      committedById: 'e-1',
      committedOn: dayjs('2026-09-03'),
    }

    // Act
    const result = toNeedByRequest(values, BASES)

    // Assert
    expect(result?.committedById).toBe('e-1')
    expect(result?.committedOn).toBe('2026-09-03')
  })
})

describe('needByFieldFor', () => {
  it('puts a need-by error on its nested field and leaves other keys alone', () => {
    // Arrange / Act / Assert
    expect(needByFieldFor('needBy.reference')).toEqual(['needBy', 'reference'])
    expect(needByFieldFor('title')).toBeUndefined()
  })
})
