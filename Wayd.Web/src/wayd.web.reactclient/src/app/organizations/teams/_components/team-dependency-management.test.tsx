import { DependencyHealth } from '@/src/components/types'
import { DependencyDto, TeamDetailsDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TeamDependencyManagement from './team-dependency-management'

let mockDependencies: DependencyDto[] = []
jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamDependenciesQuery: () => ({
    data: mockDependencies,
    isLoading: false,
    refetch: jest.fn(),
  }),
}))

// The grid's own behaviour is not under test here: only which rows the section hands it.
jest.mock('@/src/components/common/work/dependencies-grid', () => ({
  __esModule: true,
  default: (props: { dependencies: DependencyDto[]; leftSlot?: unknown }) => (
    <div>
      <ul aria-label="rows">
        {props.dependencies.map((d) => (
          <li key={d.id}>{d.id}</li>
        ))}
      </ul>
      {props.leftSlot as React.ReactNode}
    </div>
  ),
}))

const team = (id: string, name: string, code: string) => ({
  id,
  key: 1,
  name,
  code,
  type: 'Team',
})
const us = team('t-us', 'Us', 'US')
const pulsar = team('t-pulsar', 'Pulsar', 'PLS')
const quasar = team('t-quasar', 'Quasar', 'QSR')

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

const rows = () =>
  Array.from(
    screen.getByRole('list', { name: 'rows' }).querySelectorAll('li'),
    (li) => li.textContent,
  )

const renderSection = () =>
  render(<TeamDependencyManagement team={{ id: us.id } as TeamDetailsDto} />)

describe('TeamDependencyManagement', () => {
  beforeEach(() => {
    mockDependencies = [
      dependency('we-wait', pulsar, us),
      dependency('they-wait', us, pulsar),
      dependency('quasar', us, quasar),
      dependency('within', us, us),
    ]
  })

  it('narrows the list to a team picked on the chart, and clears it from the list', async () => {
    // Arrange
    renderSection()

    // Act
    await userEvent.click(screen.getByRole('button', { name: /^Pulsar:/ }))
    const pickedRows = rows()
    await userEvent.click(
      screen.getByRole('button', { name: 'Clear the team filter' }),
    )

    // Assert
    expect(pickedRows).toEqual(['we-wait', 'they-wait'])
    expect(rows()).toEqual(['we-wait', 'they-wait', 'quasar', 'within'])
  })

  it('names how much waits each way for each partner', () => {
    // Act
    renderSection()

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Pulsar: we wait on 1 (1 Healthy); they wait on us 1 (1 Healthy)',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        '1 dependency is within the team, so it is in the list but not on the chart.',
      ),
    ).toBeInTheDocument()
  })

  it('leaves the chart out when every dependency is within the team', () => {
    // Arrange
    mockDependencies = [dependency('within', us, us)]

    // Act
    renderSection()

    // Assert
    expect(
      screen.queryByText('Dependencies With Other Teams'),
    ).not.toBeInTheDocument()
    expect(rows()).toEqual(['within'])
  })
})
