import { SprintMetricFigures, sprintMetricValues } from './sprint-metric-values'

const figures: SprintMetricFigures = {
  totalWorkItems: 4,
  totalEstimate: 21,
  completedWorkItems: 2,
  completedEstimate: 13,
  inProgressWorkItems: 1,
  inProgressEstimate: 5,
  notStartedWorkItems: 1,
  notStartedEstimate: 3,
}

describe('sprintMetricValues', () => {
  it('shows the estimates by default', () => {
    // Act
    const result = sprintMetricValues(figures, false)

    // Assert
    expect(result).toEqual({
      total: 21,
      completed: 13,
      inProgress: 5,
      notStarted: 3,
    })
  })

  it('shows the item counts when by count', () => {
    // Act
    const result = sprintMetricValues(figures, true)

    // Assert
    expect(result).toEqual({
      total: 4,
      completed: 2,
      inProgress: 1,
      notStarted: 1,
    })
  })

  it('is zero while loading', () => {
    // Act
    const result = sprintMetricValues(undefined, false)

    // Assert
    expect(result).toEqual({
      total: 0,
      completed: 0,
      inProgress: 0,
      notStarted: 0,
    })
  })
})
