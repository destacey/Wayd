import { findOverlappingSprints, summarizeSprintNames } from './sprint-overlaps'

const sprint = (name: string, start: string, end: string) => ({
  name,
  start,
  end,
})

describe('findOverlappingSprints', () => {
  it('ignores sprints without planned dates', () => {
    // Arrange
    const sprints = [
      sprint('Backlog A', '', ''),
      sprint('Backlog B', '', ''),
      sprint('S1', '2026-08-03', '2026-08-16'),
    ]

    // Act
    const result = findOverlappingSprints(sprints)

    // Assert
    expect(result).toEqual([])
  })
  it('finds none when each sprint ends before the next starts', () => {
    // Arrange
    const sprints = [
      sprint('S1', '2026-08-03', '2026-08-16'),
      sprint('S2', '2026-08-17', '2026-08-30'),
    ]

    // Act
    const result = findOverlappingSprints(sprints)

    // Assert
    expect(result).toEqual([])
  })

  it('counts ending on the next start day as an overlap', () => {
    // Arrange — both ends are inclusive days, so the shared day is claimed twice.
    const sprints = [
      sprint('S1', '2026-08-03', '2026-08-17'),
      sprint('S2', '2026-08-17', '2026-08-30'),
    ]

    // Act
    const result = findOverlappingSprints(sprints)

    // Assert
    expect(result.map((s) => s.name)).toEqual(['S1', 'S2'])
  })

  it('compares neighbours in start order, not list order', () => {
    // Arrange
    const sprints = [
      sprint('S3', '2026-08-31', '2026-09-13'),
      sprint('S1', '2026-08-03', '2026-08-16'),
      sprint('S2', '2026-08-17', '2026-09-01'),
    ]

    // Act
    const result = findOverlappingSprints(sprints)

    // Assert
    expect(result.map((s) => s.name)).toEqual(['S2', 'S3'])
  })

  it('lists a sprint overlapping both neighbours once', () => {
    // Arrange
    const sprints = [
      sprint('S1', '2026-08-03', '2026-08-18'),
      sprint('S2', '2026-08-17', '2026-09-01'),
      sprint('S3', '2026-08-31', '2026-09-13'),
    ]

    // Act
    const result = findOverlappingSprints(sprints)

    // Assert
    expect(result.map((s) => s.name)).toEqual(['S1', 'S2', 'S3'])
  })
})

describe('summarizeSprintNames', () => {
  it('lists every name within the limit', () => {
    // Arrange
    const sprints = [sprint('S1', '', ''), sprint('S2', '', '')]

    // Act
    const result = summarizeSprintNames(sprints)

    // Assert
    expect(result).toBe('S1, S2')
  })

  it('counts the names past the limit', () => {
    // Arrange
    const sprints = ['S1', 'S2', 'S3', 'S4', 'S5'].map((name) =>
      sprint(name, '', ''),
    )

    // Act
    const result = summarizeSprintNames(sprints)

    // Assert
    expect(result).toBe('S1, S2, S3 and 2 more')
  })
})
