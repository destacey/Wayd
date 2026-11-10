import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PlanningIntervalDependenciesSection from './planning-interval-dependencies-section'

let mockDependencies: DependencyDto[] = []
vi.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalDependenciesQuery: () => ({
    data: mockDependencies,
    isLoading: false,
    refetch: vi.fn(),
  }),
}))

// The grid's own behaviour is not under test here: only which rows the section hands it.
vi.mock('@/src/components/common/work/dependencies-grid', () => ({
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

const team = (id: string, name: string) => ({
  id,
  key: 1,
  name,
  code: name.slice(0, 3).toUpperCase(),
  type: 'Team',
})
const atlas = team('t-atlas', 'Atlas')
const beacon = team('t-beacon', 'Beacon')
const comet = team('t-comet', 'Comet')

const dependency = (
  id: string,
  sourceTeam: ReturnType<typeof team> | undefined,
  targetTeam: ReturnType<typeof team> | undefined,
  state: 'To Do' | 'Done' = 'To Do',
): DependencyDto => {
  const item = (t: ReturnType<typeof team> | undefined) =>
    ({
      id: `${id}-${t?.id ?? 'none'}`,
      key: 'W-1',
      title: 'Item',
      workspaceKey: 'W',
      type: 'Story',
      status: 'New',
      statusCategory: { id: 1, name: 'Proposed' },
      team: t,
    }) as DependencyDto['source']
  return {
    id,
    source: item(sourceTeam),
    target: item(targetTeam),
    linkType: { id: 1, name: 'Dependency' },
    state:
      state === 'Done' ? { id: 3, name: 'Done' } : { id: 1, name: 'To Do' },
    health: { id: DependencyHealth.Healthy, name: 'Healthy' },
    scope: { id: 2, name: 'Cross-Team' },
    createdOn: new Date(),
  }
}

const rows = () =>
  Array.from(
    screen.getByRole('list', { name: 'rows' }).querySelectorAll('li'),
    (li) => li.textContent,
  )

describe('PlanningIntervalDependenciesSection', () => {
  beforeEach(() => {
    mockDependencies = [
      dependency('open-ab', atlas, beacon),
      dependency('done-ab', atlas, beacon, 'Done'),
      dependency('open-bc', beacon, comet),
      dependency('open-ca', comet, atlas),
      dependency('open-ba', beacon, atlas),
    ]
  })

  it('lists only open dependencies until All is chosen', async () => {
    // Arrange
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)
    const openRows = rows()

    // Act
    await userEvent.click(screen.getByText('All'))

    // Assert
    expect(openRows).toEqual(['open-ab', 'open-bc', 'open-ca', 'open-ba'])
    expect(rows()).toEqual([
      'open-ab',
      'done-ab',
      'open-bc',
      'open-ca',
      'open-ba',
    ])
  })

  it('narrows the list to a team picked on the wheel, and clears it from the list', async () => {
    // Arrange
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: /^Comet:/ }))
    const pickedRows = rows()
    await userEvent.click(
      screen.getByRole('button', { name: 'Clear the wheel filter' }),
    )

    // Assert
    expect(pickedRows).toEqual(['open-bc', 'open-ca'])
    expect(rows()).toEqual(['open-ab', 'open-bc', 'open-ca', 'open-ba'])
  })

  it('narrows the list to one direction when a ribbon is picked', async () => {
    // Arrange
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }),
    )

    // Assert
    expect(rows()).toEqual(['open-ab'])
    expect(screen.getByText('Atlas delivers to Beacon')).toBeInTheDocument()
  })

  it('clears the pick when the state filter changes', async () => {
    // Arrange
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)
    await userEvent.click(screen.getByText('All'))
    await userEvent.click(
      screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }),
    )
    const pickedRows = rows()

    // Act
    await userEvent.click(screen.getByText('Open'))

    // Assert
    expect(pickedRows).toEqual(['open-ab', 'done-ab'])
    expect(rows()).toEqual(['open-ab', 'open-bc', 'open-ca', 'open-ba'])
    expect(
      screen.queryByRole('button', { name: 'Clear the wheel filter' }),
    ).not.toBeInTheDocument()
  })

  it('lists dependencies between items with no team, saying they are not on the wheel', () => {
    // Arrange
    mockDependencies = [dependency('none', undefined, undefined)]

    // Act
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)

    // Assert
    expect(rows()).toEqual(['none'])
    expect(
      screen.getByText(
        '1 dependency between two items with no team is in the list but not on the wheel.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('No open dependencies cross team lines in this PI.'),
    ).not.toBeInTheDocument()
  })

  it('says so when no dependency crosses team lines', () => {
    // Arrange
    mockDependencies = []

    // Act
    render(<PlanningIntervalDependenciesSection planningIntervalKey={7} />)

    // Assert
    expect(
      screen.getByText('No open dependencies cross team lines in this PI.'),
    ).toBeInTheDocument()
  })
})
