import { SizingMethod } from '@/src/services/wayd-api'
import {
  isSizingMethod,
  sizingMethodLabel,
  sizingMethodMeasure,
  sizingMethodOptions,
} from './sizing-method'

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

describe('sizingMethodMeasure', () => {
  it.each([
    [SizingMethod.StoryPoints, 'story points'],
    [SizingMethod.Count, 'work items'],
    [SizingMethod.Effort, 'effort'],
    [SizingMethod.Size, 'size'],
  ])('describes %s as %s', (sizingMethod, measure) => {
    // Act
    const result = sizingMethodMeasure(sizingMethod)

    // Assert
    expect(result).toBe(measure)
  })
})

describe('isSizingMethod', () => {
  it.each([
    [SizingMethod.Effort, true],
    ['Custom tooltip text', false],
    [undefined, false],
  ])('recognises %s as %s', (value, expected) => {
    // Act
    const result = isSizingMethod(value)

    // Assert
    expect(result).toBe(expected)
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
