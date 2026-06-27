import {
  validateColorEntries,
  type RoadmapColorConfigEntry,
} from './roadmap-color-config'

let keyCounter = 0
const entry = (
  overrides: Partial<RoadmapColorConfigEntry> = {},
): RoadmapColorConfigEntry => ({
  key: `key-${keyCounter++}`,
  color: '#FF0000',
  name: 'At Risk',
  isDefault: false,
  ...overrides,
})

describe('validateColorEntries', () => {
  it('returns no errors for a valid set', () => {
    const entries = [
      entry({ color: '#FF0000', name: 'At Risk' }),
      entry({ color: '#00FF00', name: 'On Track' }),
    ]

    const result = validateColorEntries(entries)

    expect(result.hasErrors).toBe(false)
    expect(result.byKey).toEqual({})
  })

  it('returns no errors for an empty set', () => {
    const result = validateColorEntries([])

    expect(result.hasErrors).toBe(false)
    expect(result.byKey).toEqual({})
  })

  it('flags a missing color', () => {
    const target = entry({ color: undefined, name: 'At Risk' })

    const result = validateColorEntries([target])

    expect(result.hasErrors).toBe(true)
    expect(result.byKey[target.key]?.color).toBeDefined()
    expect(result.byKey[target.key]?.name).toBeUndefined()
  })

  it('flags a missing caption', () => {
    const target = entry({ color: '#FF0000', name: '   ' })

    const result = validateColorEntries([target])

    expect(result.hasErrors).toBe(true)
    expect(result.byKey[target.key]?.name).toBeDefined()
    expect(result.byKey[target.key]?.color).toBeUndefined()
  })

  it('flags duplicate colors on every offending row', () => {
    const first = entry({ color: '#FF0000', name: 'At Risk' })
    const second = entry({ color: '#FF0000', name: 'Blocked' })

    const result = validateColorEntries([first, second])

    expect(result.hasErrors).toBe(true)
    expect(result.byKey[first.key]?.color).toBeDefined()
    expect(result.byKey[second.key]?.color).toBeDefined()
  })

  it('treats colors that differ only by case as duplicates', () => {
    const first = entry({ color: '#abc123', name: 'Lower' })
    const second = entry({ color: '#ABC123', name: 'Upper' })

    const result = validateColorEntries([first, second])

    expect(result.hasErrors).toBe(true)
    expect(result.byKey[first.key]?.color).toBeDefined()
    expect(result.byKey[second.key]?.color).toBeDefined()
  })

  it('does not flag distinct colors as duplicates', () => {
    const entries = [
      entry({ color: '#FF0000', name: 'At Risk' }),
      entry({ color: '#FF0001', name: 'Almost At Risk' }),
    ]

    const result = validateColorEntries(entries)

    expect(result.hasErrors).toBe(false)
  })

  it('reports both a missing color and a missing caption on the same row', () => {
    const target = entry({ color: undefined, name: '' })

    const result = validateColorEntries([target])

    expect(result.byKey[target.key]?.color).toBeDefined()
    expect(result.byKey[target.key]?.name).toBeDefined()
  })
})
