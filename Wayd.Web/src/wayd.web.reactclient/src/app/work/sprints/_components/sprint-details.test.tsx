import { render, screen } from '@testing-library/react'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import SprintDetails, { sprintOverlapWarning } from './sprint-details'

jest.mock('@/src/components/common/planning/timeline-progress', () => {
  const TimelineProgress = () => <div>Timeline</div>
  return TimelineProgress
})
jest.mock('./sprint-metrics', () => {
  const SprintMetrics = () => <div>Metrics</div>
  return SprintMetrics
})
jest.mock('./sprint-scope-summary', () => {
  const SprintScopeSummary = () => <div>Scope</div>
  return SprintScopeSummary
})

const sprint: SprintDetailsDto = {
  id: 'sprint-1',
  key: 201,
  name: '26.3.2',
  state: { id: 1, name: 'Future' },
  start: '2026-08-17',
  end: '2026-08-30',
  team: { id: 't1', key: 14, name: 'Core Services', code: 'CS', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  canManageSprint: false,
  canStart: false,
  canComplete: false,
  canReopen: false,
}

describe('sprintOverlapWarning', () => {
  it('has nothing to say when the sprint overlaps neither neighbour', () => {
    // Arrange / Act
    const result = sprintOverlapWarning(sprint)

    // Assert
    expect(result).toBeNull()
  })

  it('says this sprint is cut when it overlaps the next', () => {
    // Arrange / Act
    const result = sprintOverlapWarning({ ...sprint, overlapsNextSprint: true })

    // Assert
    expect(result?.title).toBe(
      "This sprint's planned dates overlap the team's next sprint in Azure DevOps.",
    )
    expect(result?.description).toContain(
      'Unless it is completed earlier, this sprint ends when the next sprint starts, not on its planned end.',
    )
  })

  it('says the previous sprint is cut when it overlaps the previous', () => {
    // Arrange / Act
    const result = sprintOverlapWarning({
      ...sprint,
      overlapsPreviousSprint: true,
    })

    // Assert
    expect(result?.description).toContain(
      'Unless it was completed earlier, the previous sprint ends when this sprint starts, not on its planned end.',
    )
  })

  it('names both neighbours when it overlaps both', () => {
    // Arrange / Act
    const result = sprintOverlapWarning({
      ...sprint,
      overlapsPreviousSprint: true,
      overlapsNextSprint: true,
    })

    // Assert
    expect(result?.title).toContain('previous and next sprint')
  })
})

describe('SprintDetails', () => {
  it('warns about an overlap above the details', () => {
    // Arrange / Act
    render(<SprintDetails sprint={{ ...sprint, overlapsNextSprint: true }} />)

    // Assert
    expect(screen.getByRole('alert')).toHaveTextContent(
      "overlap the team's next sprint",
    )
  })

  it('shows no warning without an overlap', () => {
    // Arrange / Act
    render(<SprintDetails sprint={sprint} />)

    // Assert
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
