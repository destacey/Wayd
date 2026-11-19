import { enumFilterOptions } from './filter-options'

const Stage = {
  Draft: 'Draft',
  Active: 'Active',
  Resolved: 'Resolved',
} as const

describe('enumFilterOptions', () => {
  it('offers every code in the enum’s order, labelled by itself', () => {
    // Act
    const options = enumFilterOptions(Stage)

    // Assert
    expect(options).toEqual([
      { value: 'Draft', label: 'Draft' },
      { value: 'Active', label: 'Active' },
      { value: 'Resolved', label: 'Resolved' },
    ])
  })

  it('labels a code where its label differs, and offers only the codes named, in their order', () => {
    // Act
    const options = enumFilterOptions(Stage, { Resolved: 'Recovered' }, [
      'Resolved',
      'Draft',
    ])

    // Assert
    expect(options).toEqual([
      { value: 'Resolved', label: 'Recovered' },
      { value: 'Draft', label: 'Draft' },
    ])
  })
})
