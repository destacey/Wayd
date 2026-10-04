import { SizingMethod } from '@/src/services/wayd-api'
import { sizingMethodLabel, sizingMethodOptions } from './sizing-method'

describe('sizingMethodLabel', () => {
  it.each([
    [SizingMethod.StoryPoints, 'Story Points'],
    [SizingMethod.Count, 'Count'],
    [SizingMethod.Effort, 'Effort'],
    [SizingMethod.Size, 'Size'],
  ])('labels %s as %s', (sizingMethod, label) => {
    // Act
    const result = sizingMethodLabel(sizingMethod)

    // Assert
    expect(result).toBe(label)
  })
})

describe('sizingMethodOptions', () => {
  it('offers every sizing method', () => {
    // Act
    const values = sizingMethodOptions.map((o) => o.value)

    // Assert
    expect(values).toHaveLength(Object.values(SizingMethod).length)
    expect(values).toEqual(expect.arrayContaining(Object.values(SizingMethod)))
  })
})
