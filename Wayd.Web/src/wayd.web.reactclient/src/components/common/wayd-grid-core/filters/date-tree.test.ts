// The global dayjs shim can't strict-parse or format month names; use real dayjs.
jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { buildDateTree, dayKeysOf } from './date-tree'

describe('buildDateTree', () => {
  it('groups day keys into ascending Year → Month → Day', () => {
    // Arrange — deliberately unsorted, spanning two years and months
    const keys = [
      '2026-07-04',
      '2026-06-15',
      '2026-06-01',
      '2025-12-31',
    ]

    // Act
    const tree = buildDateTree(keys)

    // Assert — years ascending
    expect(tree.map((y) => y.key)).toEqual(['2025', '2026'])

    const y2026 = tree[1]
    expect(y2026.months.map((m) => m.key)).toEqual(['2026-06', '2026-07'])
    expect(y2026.months[0].label).toBe('June')
    expect(y2026.months[0].days.map((d) => d.key)).toEqual([
      '2026-06-01',
      '2026-06-15',
    ])
    expect(y2026.months[0].days.map((d) => d.label)).toEqual(['1', '15'])
  })

  it('ignores invalid keys', () => {
    // Act
    const tree = buildDateTree(['2026-06-15', 'not-a-date', ''])

    // Assert
    expect(dayKeysOf(tree)).toEqual(['2026-06-15'])
  })

  it('round-trips through dayKeysOf in tree order', () => {
    // Arrange
    const keys = ['2026-01-02', '2026-01-01', '2025-11-30']

    // Act
    const flat = dayKeysOf(buildDateTree(keys))

    // Assert — chronological, deduped by structure
    expect(flat).toEqual(['2025-11-30', '2026-01-01', '2026-01-02'])
  })
})
