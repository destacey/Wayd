import { render, screen } from '@testing-library/react'
import ProjectStatPills from './project-stat-pills'
import { ProjectPlanSummaryDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

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
    const { container } = render(<ProjectStatPills />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when all counts are 0', () => {
    const { container } = render(<ProjectStatPills summary={createSummary()} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders overdue pill when overdue > 0', () => {
    render(<ProjectStatPills summary={createSummary({ overdue: 3 })} />)

    expect(screen.getByText('3 overdue')).toBeInTheDocument()
  })

  it('renders due this week pill when dueThisWeek > 0', () => {
    render(<ProjectStatPills summary={createSummary({ dueThisWeek: 5 })} />)

    expect(screen.getByText('5 this week')).toBeInTheDocument()
  })

  it('renders upcoming pill when upcoming > 0', () => {
    render(<ProjectStatPills summary={createSummary({ upcoming: 2 })} />)

    expect(screen.getByText('2 upcoming')).toBeInTheDocument()
  })

  it('renders all pills when all counts > 0', () => {
    render(
      <ProjectStatPills
        summary={createSummary({ overdue: 1, dueThisWeek: 2, upcoming: 3 })}
      />,
    )

    expect(screen.getByText('1 overdue')).toBeInTheDocument()
    expect(screen.getByText('2 this week')).toBeInTheDocument()
    expect(screen.getByText('3 upcoming')).toBeInTheDocument()
  })
})
