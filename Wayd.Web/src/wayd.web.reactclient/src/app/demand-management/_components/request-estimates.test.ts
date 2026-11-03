import { IntakeRequestSummaryDto } from '@/src/services/wayd-api'
import {
  compareByEstimate,
  compareEstimates,
  toRequestEstimates,
} from './request-estimates'

const estimate = (
  value: string,
  position: number,
  versionId = 'v-1',
  scaleName = 'T-Shirt Sizes',
  versionNumber = 1,
) => ({
  value,
  position,
  scale: {
    id: `s-${scaleName}`,
    key: 1,
    name: scaleName,
    versionId,
    versionNumber,
    isCurrentVersion: true,
  },
})

const request = (id: string, key: number) =>
  ({ id, key }) as IntakeRequestSummaryDto

describe('compareEstimates', () => {
  it('orders by position on the scale, not by the text, with no estimate last', () => {
    // Arrange
    const estimates = [undefined, estimate('XL', 4), estimate('S', 1)]

    // Act
    const ordered = [...estimates].sort(compareEstimates).map((e) => e?.value)

    // Assert
    expect(ordered).toEqual(['S', 'XL', undefined])
  })

  it('keeps each version in a run of its own, by scale name and then version number', () => {
    // Arrange
    const estimates = [
      estimate('XL', 4, 'v-2', 'T-Shirt Sizes', 2),
      estimate('13', 6, 'v-f', 'Fibonacci'),
      estimate('S', 1, 'v-1', 'T-Shirt Sizes', 1),
      estimate('XS', 0, 'v-2', 'T-Shirt Sizes', 2),
    ]

    // Act
    const ordered = [...estimates].sort(compareEstimates).map((e) => e.value)

    // Assert
    expect(ordered).toEqual(['13', 'S', 'XS', 'XL'])
  })
})

describe('compareByEstimate', () => {
  it('puts the largest first, then the newest of equal ones, and requests with no estimate last', () => {
    // Arrange
    const estimates = toRequestEstimates([
      { requestId: 'small', estimate: estimate('S', 1) },
      { requestId: 'large-old', estimate: estimate('L', 3) },
      { requestId: 'large-new', estimate: estimate('L', 3) },
    ])
    const requests = [
      request('none-old', 1),
      request('small', 2),
      request('large-old', 3),
      request('none-new', 4),
      request('large-new', 5),
    ]

    // Act
    const ordered = [...requests]
      .sort(compareByEstimate(estimates))
      .map((r) => r.id)

    // Assert
    expect(ordered).toEqual([
      'large-new',
      'large-old',
      'small',
      'none-new',
      'none-old',
    ])
  })
})
