import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ProjectsCardView from './projects-card-view'
import { ProjectListDto } from '@/src/services/wayd-api'

jest.mock('antd', () => ({
  Card: ({ children, onClick }: any) => <div onClick={onClick}>{children}</div>,
  Flex: ({ children }: any) => <div>{children}</div>,
  Segmented: ({ value, onChange, options }: any) => (
    <div>
      {options.map((option: any) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={value === option.value}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  ),
  Spin: () => <div data-testid="spin" />,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}))

jest.mock('@/src/components/common', () => ({
  LifecycleStatusTag: () => <span>Status</span>,
}))

jest.mock('@/src/components/common/planning/timeline-progress', () => {
  const MockTimelineProgress = () => <div data-testid="timeline-progress" />
  MockTimelineProgress.displayName = 'MockTimelineProgress'
  return MockTimelineProgress
})

jest.mock('./phase-timeline', () => {
  const MockPhaseTimeline = () => <div data-testid="phase-timeline" />
  MockPhaseTimeline.displayName = 'MockPhaseTimeline'
  return MockPhaseTimeline
})

jest.mock('../projects/_components', () => ({
  ProjectHealthCheckTag: () => null,
}))

function createProject(
  overrides: Partial<ProjectListDto> & { key: string; name: string },
): ProjectListDto {
  return {
    id: overrides.key,
    key: overrides.key,
    name: overrides.name,
    status: { id: 1, name: 'Active' } as any,
    portfolio: { id: 'portfolio-1', key: 1, name: 'Portfolio' } as any,
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    phases: [],
    rank: 0,
    canManageProject: true,
    ...overrides,
  } as ProjectListDto
}

function getProjectLinkNames() {
  return screen.getAllByRole('link').map((link) => link.textContent)
}

describe('ProjectsCardView', () => {
  const projects = [
    createProject({ key: 'BETA', name: 'Beta Project', position: 2 }),
    createProject({ key: 'ALPHA', name: 'Alpha Project', position: 3 }),
    createProject({ key: 'GAMMA', name: 'Gamma Project', position: 1 }),
  ]

  it('sorts projects by name by default', () => {
    render(
      <ProjectsCardView
        projects={projects}
        isLoading={false}
        onCardClick={jest.fn()}
      />,
    )

    expect(getProjectLinkNames()).toEqual([
      'Alpha Project',
      'Beta Project',
      'Gamma Project',
    ])
  })

  it('sorts projects by rank when selected', async () => {
    render(
      <ProjectsCardView
        projects={projects}
        isLoading={false}
        onCardClick={jest.fn()}
      />,
    )

    await userEvent.click(screen.getByText('by rank'))

    expect(getProjectLinkNames()).toEqual([
      'Gamma Project',
      'Beta Project',
      'Alpha Project',
    ])
  })
})
