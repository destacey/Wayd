import { render, screen } from '@testing-library/react'
import ProjectStatPills from './project-stat-pills'
import { ProjectPlanSummaryDto } from '@/src/services/wayd-api'

function createSummary(
  overrides?: Partial<ProjectPlanSummaryDto>,
): ProjectPlanSummaryDto {
  return {
    overdue: 0,
    dueThisWeek: 0,
    upcoming: 0,
    totalLeafTasks: 10,
    ...overrides,
  }
}

describe('ProjectStatPills', () => {
  it('renders nothing when summary is undefined', () => {
    // Arrange / Act
    const { container } = render(<ProjectStatPills />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when all counts are 0', () => {
    // Arrange / Act
    const { container } = render(<ProjectStatPills summary={createSummary()} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders the overdue and due-this-week pills together', () => {
    // Arrange / Act
    render(
      <ProjectStatPills
        summary={createSummary({ overdue: 3, dueThisWeek: 5 })}
      />,
    )

    // Assert
    expect(screen.getByText('3 overdue')).toBeInTheDocument()
    expect(screen.getByText('5 this week')).toBeInTheDocument()
  })

  it('shows the upcoming count only when nothing more urgent is on the row', () => {
    // Arrange / Act
    render(<ProjectStatPills summary={createSummary({ upcoming: 2 })} />)
    const { container: busy } = render(
      <ProjectStatPills summary={createSummary({ overdue: 1, upcoming: 3 })} />,
    )

    // Assert
    expect(screen.getByText('2 upcoming')).toBeInTheDocument()
    expect(busy).not.toHaveTextContent('3 upcoming')
  })
})
