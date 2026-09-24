import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  ScopedDependencyDto,
  WorkItemDetailsDto,
  WorkItemDetailsNavigationDto,
} from '@/src/services/wayd-api'
import type {
  DependencyNode,
  DependencyNodeData,
} from '@/src/components/common/dependency-map/dependency-neighbourhood'
import { expansionStorageKey } from '@/src/components/common/dependency-map/use-dependency-map-expansions'
import { useExpandedWorkItemDependencies } from './use-expanded-work-item-dependencies'
import { WORK_ITEM_DEPENDENCY_FILTER_KEY } from './use-work-item-dependency-filter'
import WorkItemDependencyMapCard from './work-item-dependency-map-card'

// The canvas measures itself and reads the theme provider, neither of which this test supplies. The stub
// renders what the card hands it and exposes the expand buttons; the map's own tests cover drawing.
jest.mock('@/src/components/common/dependency-map/dependency-map', () => ({
  __esModule: true,
  default: ({
    nodes,
    edges,
    edgeStyles,
    filters,
    emptyText,
    onToggleExpansion,
  }: {
    nodes: DependencyNode[]
    edges: { id: string; data?: { variant: string } }[]
    edgeStyles: Record<string, unknown>
    filters?: React.ReactNode
    emptyText?: React.ReactNode
    onToggleExpansion?: (side: string, recordId: string) => void
  }) => (
    <div data-testid="dependency-map">
      {filters}
      <span data-testid="map-edges">{edges.map((e) => e.id).join(',')}</span>
      <span data-testid="unstyled-edges">
        {edges.filter((e) => !edgeStyles[e.data!.variant]).length}
      </span>
      {nodes.map((node) => {
        const data = node.data as DependencyNodeData
        if (node.type !== 'record' || !data.expansion) return null
        return (
          <button
            key={node.id}
            onClick={() => onToggleExpansion?.(data.side, data.recordId)}
          >
            {`Toggle ${data.label}`}
          </button>
        )
      })}
      {nodes.length === 0 && emptyText}
    </div>
  ),
}))

jest.mock('./use-expanded-work-item-dependencies', () => ({
  useExpandedWorkItemDependencies: jest.fn(),
}))

const workItem = {
  id: 'w-1',
  key: 'APP-1',
  title: 'Checkout',
  workspace: { id: 'ws-1', key: 'APP', name: 'App' },
} as unknown as WorkItemDetailsDto

const item = (id: string, key: string): WorkItemDetailsNavigationDto => ({
  id,
  key,
  title: `Title ${key}`,
  workspaceKey: key.split('-')[0],
  type: 'Story',
  status: 'Active',
  statusCategory: { id: 2, name: 'Active' },
})

const auth = item('w-2', 'ID-7')
const refund = item('w-3', 'APP-9')

const dependency = (
  id: string,
  other: WorkItemDetailsNavigationDto,
  type: 'Predecessor' | 'Successor',
  state = 'To Do',
  health = 'Healthy',
): ScopedDependencyDto => ({
  id,
  dependency: other,
  type,
  state: { id: 1, name: state },
  health: { id: 1, name: health },
  scope: { id: 2, name: 'Cross-Team' },
  createdOn: new Date('2026-01-01'),
})

const renderCard = (
  dependencies: ScopedDependencyDto[],
  onViewAll = jest.fn(),
) =>
  render(
    <WorkItemDependencyMapCard
      workItem={workItem}
      dependencies={dependencies}
      onViewAll={onViewAll}
    />,
  )

// An in-memory session, so what the card writes is what it reads back.
let session: Record<string, string>
const getStored = jest.mocked(window.sessionStorage.getItem)
const setStored = jest.mocked(window.sessionStorage.setItem)
const expanded = jest.mocked(useExpandedWorkItemDependencies)

describe('WorkItemDependencyMapCard', () => {
  beforeEach(() => {
    session = {}
    getStored.mockImplementation((key: string) => session[key] ?? null)
    setStored.mockImplementation((key: string, value: string) => {
      session[key] = value
    })
    expanded.mockReturnValue({})
  })

  afterEach(() => {
    getStored.mockReset()
    setStored.mockReset()
    expanded.mockReset()
  })

  it('is absent when the work item has no dependencies', () => {
    // Act
    renderCard([])

    // Assert
    expect(screen.queryByTestId('dependency-map')).not.toBeInTheDocument()
  })

  it('draws every link, each in a style the card defines', async () => {
    // Arrange
    renderCard([
      dependency('d1', auth, 'Predecessor', 'To Do', 'Unhealthy'),
      dependency('d2', refund, 'Successor', 'Done'),
    ])

    // Act
    const edges = await screen.findByTestId('map-edges')

    // Assert
    expect(edges).toHaveTextContent('d2,d1')
    expect(screen.getByTestId('unstyled-edges')).toHaveTextContent('0')
  })

  it('keys every line style in the legend', async () => {
    // Act
    renderCard([dependency('d1', auth, 'Predecessor')])

    // Assert
    await screen.findByTestId('dependency-map')
    for (const label of [
      'Healthy',
      'At Risk',
      'Unhealthy',
      'Unknown',
      'Done',
    ]) {
      expect(screen.getByText(label)).toBeInTheDocument()
    }
  })

  it('draws only open links, and remembers the choice for the session', async () => {
    // Arrange
    const user = userEvent.setup()
    renderCard([
      dependency('open', auth, 'Predecessor'),
      dependency('done', refund, 'Successor', 'Done'),
    ])

    // Act
    await user.click(await screen.findByText('Open only'))

    // Assert
    expect(screen.getByTestId('map-edges')).toHaveTextContent(/^open$/)
    expect(session[WORK_ITEM_DEPENDENCY_FILTER_KEY]).toBe('open')
    // A key to a line the map no longer draws would only mislead.
    expect(screen.queryByText('Done')).not.toBeInTheDocument()
  })

  it('keeps the map, and its filter, when every link is done', async () => {
    // Arrange
    session[WORK_ITEM_DEPENDENCY_FILTER_KEY] = 'open'

    // Act
    renderCard([dependency('done', auth, 'Predecessor', 'Done')])

    // Assert
    // Hiding the card here would take away the only way to turn the filter back off.
    expect(
      await screen.findByText('No open dependencies in either direction.'),
    ).toBeInTheDocument()
    expect(screen.getByText('All')).toBeInTheDocument()
  })

  it('follows a predecessor outward and keeps the expansion for the session', async () => {
    // Arrange
    const user = userEvent.setup()
    const keys = item('w-5', 'SEC-2')
    expanded.mockImplementation((_, ids) =>
      Object.fromEntries(
        ids.map((id) => [
          id,
          {
            workItem: auth,
            dependencies: [dependency('beyond', keys, 'Predecessor')],
          },
        ]),
      ),
    )
    renderCard([dependency('d1', auth, 'Predecessor')])

    // Act
    await user.click(
      await screen.findByRole('button', {
        name: 'Toggle ID-7 · Title ID-7',
      }),
    )

    // Assert
    expect(expanded).toHaveBeenLastCalledWith(expect.any(Array), [auth.id])
    expect(screen.getByTestId('map-edges')).toHaveTextContent('d1,beyond')
    expect(JSON.parse(session[expansionStorageKey(workItem.id)]).left).toEqual({
      [auth.id]: {},
    })
  })

  it('opens the Dependencies section from View all', async () => {
    // Arrange
    const user = userEvent.setup()
    const onViewAll = jest.fn()
    renderCard([dependency('d1', auth, 'Predecessor')], onViewAll)

    // Act
    await user.click(await screen.findByText('View all'))

    // Assert
    expect(onViewAll).toHaveBeenCalled()
  })
})
