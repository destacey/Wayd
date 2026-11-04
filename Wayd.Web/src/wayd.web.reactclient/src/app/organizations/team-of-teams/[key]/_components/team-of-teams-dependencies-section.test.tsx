import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import TeamOfTeamsDependenciesSection from './team-of-teams-dependencies-section'

let mockDependencies: DependencyDto[] = []
const mockQuery = jest.fn()
jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOfTeamsDependenciesQuery: (id: string) => {
    mockQuery(id)
    return { data: mockDependencies, isLoading: false, refetch: jest.fn() }
  },
}))

jest.mock('@/src/components/common/work/dependencies-grid', () => ({
  __esModule: true,
  default: (props: { dependencies: DependencyDto[] }) => (
    <ul aria-label="rows">
      {props.dependencies.map((d) => (
        <li key={d.id}>{d.id}</li>
      ))}
    </ul>
  ),
}))

const team = (id: string, name: string, code: string) => ({
  id,
  key: 1,
  name,
  code,
  type: 'Team',
})

const dependency = (
  id: string,
  sourceTeam: ReturnType<typeof team>,
  targetTeam: ReturnType<typeof team>,
): DependencyDto =>
  ({
    id,
    source: { id: `${id}-s`, team: sourceTeam },
    target: { id: `${id}-t`, team: targetTeam },
    state: { id: 1, name: 'To Do' },
    health: { id: DependencyHealth.Healthy, name: 'Healthy' },
  }) as DependencyDto

describe('TeamOfTeamsDependenciesSection', () => {
  it("draws the team of teams' dependencies on the wheel and lists them", () => {
    // Arrange
    mockDependencies = [
      dependency('ab', team('a', 'Atlas', 'ATL'), team('b', 'Beacon', 'BCN')),
    ]

    // Act
    render(
      <TeamOfTeamsDependenciesSection
        teamOfTeamsId="tot-1"
        teamOfTeamsKey={4}
      />,
    )

    // Assert
    expect(mockQuery).toHaveBeenCalledWith('tot-1')
    expect(
      screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }),
    ).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'rows' })).toHaveTextContent('ab')
  })

  it('says so when no dependency crosses team lines', () => {
    // Arrange
    mockDependencies = []

    // Act
    render(
      <TeamOfTeamsDependenciesSection
        teamOfTeamsId="tot-1"
        teamOfTeamsKey={4}
      />,
    )

    // Assert
    expect(
      screen.getByText(
        'No open dependencies cross team lines for these teams.',
      ),
    ).toBeInTheDocument()
  })
})
