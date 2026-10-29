import { compareTags, tagText } from './tag-text'

describe('tagText', () => {
  it('qualifies a tag on an axis with the axis', () => {
    // Act
    const text = tagText({ name: 'ios', categoryName: 'Platform' })

    // Assert
    expect(text).toBe('Platform | ios')
  })

  it('leaves a free tag as its name', () => {
    // Act
    const text = tagText({ name: 'migration wave' })

    // Assert
    expect(text).toBe('migration wave')
  })
})

describe('compareTags', () => {
  it('puts tags on axes first, by axis then name, and free tags after by name', () => {
    // Arrange
    const tags = [
      { name: 'zeta' },
      { name: 'web', categoryName: 'Platform' },
      { name: 'Alpha' },
      { name: 'gold', categoryName: 'Tier' },
      { name: 'android', categoryName: 'platform' },
    ]

    // Act
    const sorted = [...tags].sort(compareTags).map(tagText)

    // Assert
    expect(sorted).toEqual([
      'platform | android',
      'Platform | web',
      'Tier | gold',
      'Alpha',
      'zeta',
    ])
  })
})
