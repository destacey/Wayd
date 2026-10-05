import { SizingMethod } from '@/src/services/wayd-api'
import {
  WorkItemEstimates,
  defaultEstimate,
  estimateColumns,
} from './estimate-columns'

const hiddenByDefault = (sizingMethod?: SizingMethod | null) =>
  Object.fromEntries(
    estimateColumns<WorkItemEstimates>(sizingMethod).map((c) => [
      c.id,
      c.meta?.hiddenByDefault,
    ]),
  )

describe('defaultEstimate', () => {
  it.each([
    [SizingMethod.Effort, SizingMethod.Effort],
    [SizingMethod.Size, SizingMethod.Size],
    [SizingMethod.StoryPoints, SizingMethod.StoryPoints],
    [SizingMethod.Count, SizingMethod.StoryPoints],
    [undefined, SizingMethod.StoryPoints],
    [null, SizingMethod.StoryPoints],
  ])('follows %s with %s', (sizingMethod, expected) => {
    // Act
    const result = defaultEstimate(sizingMethod)

    // Assert
    expect(result).toBe(expected)
  })
})

describe('estimateColumns', () => {
  it("shows only the team's estimate", () => {
    // Act
    const result = hiddenByDefault(SizingMethod.Effort)

    // Assert
    expect(result).toEqual({ storyPoints: true, effort: false, size: true })
  })

  it('shows story points when there is no estimate to follow', () => {
    // Act
    const result = hiddenByDefault(undefined)

    // Assert
    expect(result).toEqual({ storyPoints: false, effort: true, size: true })
  })

  it('reads each estimate from its own field', () => {
    // Arrange
    const row: WorkItemEstimates = { storyPoints: 3, effort: 8, size: 20 }

    // Act
    const values = estimateColumns<WorkItemEstimates>(SizingMethod.Size).map(
      (c) =>
        (
          c as unknown as { accessorFn: (r: WorkItemEstimates) => unknown }
        ).accessorFn(row),
    )

    // Assert
    expect(values).toEqual([3, 8, 20])
  })
})
