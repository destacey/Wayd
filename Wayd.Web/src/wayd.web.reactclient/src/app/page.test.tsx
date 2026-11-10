import {
  HomeContextResponse,
  HomeLayoutColumn,
  HomeLayoutWidgetResponse,
} from '@/src/services/wayd-api'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { homeContext } from './_components/home/home-context-test-utils'
import HomePage from './page'

const mockAuth = {
  employeeId: 'emp-1' as string | null,
  claims: new Set<string>(),
  flags: new Set<string>(),
  flagsLoading: false,
}

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    user: { name: 'Alex Rivera', employeeId: mockAuth.employeeId },
    hasPermissionClaim: (claim: string) => mockAuth.claims.has(claim),
  }),
}))

vi.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
  useFeatureFlags: () => ({
    isEnabled: (name: string) => mockAuth.flags.has(name),
    isLoading: mockAuth.flagsLoading,
  }),
}))

vi.mock('@/src/hooks/use-document-title', () => ({
  useDocumentTitle: vi.fn(),
}))

const mockLayout = {
  widgets: [] as HomeLayoutWidgetResponse[],
  isLoading: false,
  loadFails: false,
  saveFails: false,
}
const mockUpdateHomeLayout = vi.fn()
const mockMessage = { error: vi.fn() }

vi.mock('@/src/store/features/user-management/profile-api', () => ({
  useGetHomeLayoutQuery: () => ({
    data:
      mockLayout.isLoading || mockLayout.loadFails
        ? undefined
        : { widgets: mockLayout.widgets },
    isLoading: mockLayout.isLoading,
    isError: mockLayout.loadFails,
  }),
  useUpdateHomeLayoutMutation: () => [
    (request: unknown) => {
      mockUpdateHomeLayout(request)
      return {
        unwrap: () =>
          mockLayout.saveFails
            ? Promise.reject(new Error('save failed'))
            : Promise.resolve(null),
      }
    },
  ],
}))

const mockHome = {
  context: undefined as HomeContextResponse | undefined,
  isLoading: false,
}

vi.mock('./_components/home/use-home-context', () => ({
  useHomeContext: () => mockHome,
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

// antd's Drawer reaches for Next's server-side AsyncLocalStorage, which jsdom
// has no runtime for. Only the Drawer is stubbed, so its rows stay real.
vi.mock('antd', async () => {
  const actual = await vi.importActual<any>('antd')
  const MockDrawer = ({ open, footer, children }: any) =>
    open ? (
      <div data-testid="customize-drawer">
        {children}
        {footer}
      </div>
    ) : null
  MockDrawer.displayName = 'MockDrawer'
  return { ...actual, Drawer: MockDrawer }
})

// Real gating and layout over stand-in widgets, so the page test sees what the
// registry decides without each widget's requests. Each stand-in shows the
// title the page hands it.
vi.mock('./_components/home/home-widgets', async () => {
  const { lazy } = await vi.importActual<typeof import('react')>('react')
  const stand = (label: string) => {
    const Stand = ({ title }: { title?: string }) => (
      <div>
        {label}
        {title ? ` (${title})` : ''}
      </div>
    )
    Stand.displayName = label
    return Stand
  }
  return {
    HomeWidgets: [
      {
        id: 'personal',
        title: 'Personal',
        description: '',
        defaultColumn: 'main',
        defaultOrder: 10,
        requires: {
          linkedEmployee: true,
          permissions: ['Permissions.Projects.View'],
        },
        Component: stand('Personal widget'),
        tiles: [{ id: 'personal-tile', Tile: stand('Personal tile') }],
      },
      {
        id: 'flagged',
        title: 'Flagged',
        description: '',
        defaultColumn: 'side',
        defaultOrder: 10,
        requires: { featureFlag: 'following' },
        Component: stand('Flagged widget'),
      },
      {
        id: 'permitted',
        title: 'Permitted',
        description: '',
        defaultColumn: 'main',
        defaultOrder: 20,
        requires: { permissions: ['Permissions.PlanningIntervals.View'] },
        Component: stand('Permitted widget'),
        // Leads, so moves to the top of main, for a member of a team.
        involvement: (context: HomeContextResponse) =>
          context.teams.teamCount > 0 ? 3 : 1,
      },
      {
        id: 'lazy',
        title: 'Lazy',
        description: '',
        defaultColumn: 'side',
        defaultOrder: 20,
        requires: { permissions: ['Permissions.PlanningIntervals.View'] },
        Component: lazy(
          () =>
            new Promise<{ default: ReturnType<typeof stand> }>((resolve) =>
              setTimeout(() => resolve({ default: stand('Lazy widget') }), 20),
            ),
        ),
      },
    ],
  }
})

describe('HomePage', () => {
  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    mockAuth.claims = new Set([
      'Permissions.Projects.View',
      'Permissions.PlanningIntervals.View',
    ])
    mockAuth.flags = new Set(['following'])
    mockAuth.flagsLoading = false
    mockLayout.widgets = []
    mockLayout.isLoading = false
    mockLayout.saveFails = false
    mockLayout.loadFails = false
    mockHome.context = undefined
    mockHome.isLoading = false
    mockUpdateHomeLayout.mockClear()
    mockMessage.error.mockClear()
  })

  it('shows nothing of a flagged widget until its flag is known to be on', () => {
    // Arrange
    mockAuth.flagsLoading = true
    mockAuth.flags = new Set()

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.queryByText(/Flagged/)).not.toBeInTheDocument()
    expect(screen.getByText(/Personal widget/)).toBeInTheDocument()
  })

  it('shows the widget title over a skeleton while a lazy widget loads', async () => {
    // Arrange
    mockAuth.flags = new Set()
    mockAuth.claims = new Set(['Permissions.PlanningIntervals.View'])

    // Act
    const { container } = render(<HomePage />)

    // Assert
    expect(screen.getByText('Lazy')).toBeInTheDocument()
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()

    // Act — the widget's code arrives
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50))
    })

    // Assert
    expect(screen.getByText(/Lazy widget/)).toBeInTheDocument()
  })

  it('greets the user by first name', () => {
    // Arrange / Act
    render(<HomePage />)

    // Assert
    expect(
      screen.getByRole('heading', {
        name: /^Good (morning|afternoon|evening), Alex$/,
      }),
    ).toBeInTheDocument()
  })

  it('shows every widget and tile whose requirements hold, and no banner', () => {
    // Arrange / Act
    render(<HomePage />)

    // Assert
    expect(screen.getByText('Personal widget (Personal)')).toBeInTheDocument()
    expect(screen.getByText('Flagged widget (Flagged)')).toBeInTheDocument()
    expect(screen.getByText('Permitted widget (Permitted)')).toBeInTheDocument()
    expect(screen.getByText('Personal tile')).toBeInTheDocument()
    expect(
      screen.queryByText(/isn't linked to an employee record/),
    ).not.toBeInTheDocument()
  })

  it('hides a widget whose flag is off or permission is missing', () => {
    // Arrange
    mockAuth.flags = new Set()
    mockAuth.claims = new Set(['Permissions.Projects.View'])

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.getByText(/Personal widget/)).toBeInTheDocument()
    expect(screen.queryByText(/Flagged widget/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Permitted widget/)).not.toBeInTheDocument()
  })

  it('explains to an unlinked account what will appear, and drops personal widgets', () => {
    // Arrange
    mockAuth.employeeId = null

    // Act
    render(<HomePage />)

    // Assert
    expect(
      screen.getByText(/isn't linked to an employee record/),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/projects, risks, tasks and teams/),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Personal widget/)).not.toBeInTheDocument()
    expect(screen.queryByText('Personal tile')).not.toBeInTheDocument()
    expect(screen.getByText(/Permitted widget/)).toBeInTheDocument()
  })

  it('says so, rather than going blank, when nothing applies to a linked account', () => {
    // Arrange
    mockAuth.flags = new Set()
    mockAuth.claims = new Set()

    // Act
    render(<HomePage />)

    // Assert
    expect(
      screen.getByText(/Nothing on the home page applies to your account yet/),
    ).toBeInTheDocument()
  })

  it('shows an unlinked account with nothing else the banner, not an empty page', () => {
    // Arrange
    mockAuth.employeeId = null
    mockAuth.flags = new Set()
    mockAuth.claims = new Set()

    // Act
    render(<HomePage />)

    // Assert
    expect(
      screen.getByText(/isn't linked to an employee record/),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(/Nothing on the home page applies/),
    ).not.toBeInTheDocument()
  })
})

describe('HomePage customizing', () => {
  const saved = (
    widgetId: string,
    column: HomeLayoutColumn,
    visible = true,
  ): HomeLayoutWidgetResponse => ({ widgetId, column, visible })

  /** The page's widgets, top to bottom, main column first. */
  const pageWidgets = () =>
    screen
      .queryAllByText(/ widget \(/)
      .filter((el) => !el.closest('[data-testid="customize-drawer"]'))
      .map((el) => el.textContent)

  const openCustomize = () =>
    fireEvent.click(screen.getByRole('button', { name: /Customize/ }))

  const closeCustomize = async () => {
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Done' }))
    })
  }

  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    mockAuth.claims = new Set(['Permissions.Projects.View'])
    mockAuth.flags = new Set(['following'])
    mockAuth.flagsLoading = false
    mockLayout.widgets = []
    mockLayout.isLoading = false
    mockLayout.saveFails = false
    mockLayout.loadFails = false
    mockHome.context = undefined
    mockHome.isLoading = false
    mockUpdateHomeLayout.mockClear()
    mockMessage.error.mockClear()
  })

  it('arranges the page as the saved layout says', () => {
    // Arrange: Flagged moved to main above Personal.
    mockLayout.widgets = [
      saved('flagged', HomeLayoutColumn.Main),
      saved('personal', HomeLayoutColumn.Main),
    ]

    // Act
    render(<HomePage />)

    // Assert
    expect(pageWidgets()).toEqual([
      'Flagged widget (Flagged)',
      'Personal widget (Personal)',
    ])
  })

  it('leaves out a hidden widget and its tiles', () => {
    // Arrange
    mockLayout.widgets = [saved('personal', HomeLayoutColumn.Main, false)]

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.queryByText(/Personal widget/)).not.toBeInTheDocument()
    expect(screen.queryByText('Personal tile')).not.toBeInTheDocument()
    expect(screen.getByText(/Flagged widget/)).toBeInTheDocument()
  })

  it('orders the default by what the caller is involved in', () => {
    // Arrange
    mockAuth.claims.add('Permissions.PlanningIntervals.View')
    mockHome.context = homeContext({ teams: { teamCount: 1 } })

    // Act
    render(<HomePage />)

    // Assert
    expect(pageWidgets().slice(0, 2)).toEqual([
      'Permitted widget (Permitted)',
      'Personal widget (Personal)',
    ])
  })

  it('keeps a saved layout over the involvement order', () => {
    // Arrange
    mockAuth.claims.add('Permissions.PlanningIntervals.View')
    mockHome.context = homeContext({ teams: { teamCount: 1 } })
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main),
      saved('permitted', HomeLayoutColumn.Main),
    ]

    // Act
    render(<HomePage />)

    // Assert
    expect(pageWidgets().slice(0, 2)).toEqual([
      'Personal widget (Personal)',
      'Permitted widget (Permitted)',
    ])
  })

  it('draws no widget until the involvement the default is ordered by has loaded', () => {
    // Arrange
    mockHome.isLoading = true

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.queryByText(/widget/)).not.toBeInTheDocument()
  })

  it('draws no widget or tile until the saved layout has loaded', () => {
    // Arrange
    mockLayout.isLoading = true

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.queryByText(/widget/)).not.toBeInTheDocument()
    expect(screen.queryByText('Personal tile')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Customize/ })).toBeDisabled()
  })

  it('falls back to the default but offers no Customize when the saved layout fails to load', () => {
    // Arrange
    mockLayout.loadFails = true

    // Act
    render(<HomePage />)

    // Assert — customizing would overwrite the saved layout with the default
    expect(pageWidgets()).toEqual([
      'Personal widget (Personal)',
      'Flagged widget (Flagged)',
    ])
    expect(screen.getByRole('button', { name: /Customize/ })).toBeDisabled()
  })

  it('offers no Customize until the feature flags have loaded', () => {
    // Arrange
    mockAuth.flagsLoading = true

    // Act
    render(<HomePage />)

    // Assert
    expect(screen.getByRole('button', { name: /Customize/ })).toBeDisabled()
  })

  it('follows each change on the page and saves when the drawer closes', async () => {
    // Arrange
    render(<HomePage />)
    openCustomize()

    // Act
    fireEvent.click(screen.getByRole('switch', { name: 'Show Personal' }))

    // Assert — the page follows at once; nothing is saved yet
    expect(pageWidgets()).toEqual(['Flagged widget (Flagged)'])
    expect(screen.queryByText('Personal tile')).not.toBeInTheDocument()
    expect(mockUpdateHomeLayout).not.toHaveBeenCalled()

    // Act
    await closeCustomize()

    // Assert
    expect(mockUpdateHomeLayout).toHaveBeenCalledTimes(1)
    expect(mockUpdateHomeLayout).toHaveBeenCalledWith({
      widgets: [
        saved('personal', HomeLayoutColumn.Main, false),
        saved('flagged', HomeLayoutColumn.Side),
      ],
    })
    expect(mockMessage.error).not.toHaveBeenCalled()
  })

  it('saves nothing when the drawer closes unchanged', async () => {
    // Arrange
    render(<HomePage />)
    openCustomize()

    // Act
    await closeCustomize()

    // Assert
    expect(mockUpdateHomeLayout).not.toHaveBeenCalled()
  })

  it('clears the saved layout on Reset to default', async () => {
    // Arrange
    mockLayout.widgets = [saved('personal', HomeLayoutColumn.Side, false)]
    render(<HomePage />)
    openCustomize()

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Reset to default' }))

    // Assert — the default shows at once
    expect(pageWidgets()).toEqual([
      'Personal widget (Personal)',
      'Flagged widget (Flagged)',
    ])

    // Act
    await closeCustomize()

    // Assert
    expect(mockUpdateHomeLayout).toHaveBeenCalledWith({ widgets: [] })
  })

  it('resets to the involvement order, not the registry defaults', () => {
    // Arrange
    mockAuth.claims.add('Permissions.PlanningIntervals.View')
    mockHome.context = homeContext({ teams: { teamCount: 1 } })
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main),
      saved('permitted', HomeLayoutColumn.Side),
    ]
    render(<HomePage />)
    openCustomize()

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Reset to default' }))

    // Assert
    expect(pageWidgets().slice(0, 2)).toEqual([
      'Permitted widget (Permitted)',
      'Personal widget (Personal)',
    ])
  })

  it('keeps a saved widget the account cannot use when saving', async () => {
    // Arrange: the following flag is off, so Flagged is neither shown nor offered.
    mockAuth.flags = new Set()
    mockLayout.widgets = [
      saved('flagged', HomeLayoutColumn.Main, false),
      saved('personal', HomeLayoutColumn.Main),
      saved('retired', HomeLayoutColumn.Side),
    ]
    render(<HomePage />)
    openCustomize()
    expect(
      screen.queryByRole('switch', { name: 'Show Flagged' }),
    ).not.toBeInTheDocument()

    // Act
    fireEvent.click(screen.getByRole('switch', { name: 'Show Personal' }))
    await closeCustomize()

    // Assert
    expect(mockUpdateHomeLayout).toHaveBeenCalledWith({
      widgets: [
        saved('flagged', HomeLayoutColumn.Main, false),
        saved('personal', HomeLayoutColumn.Main, false),
      ],
    })
  })

  it('says so when the layout cannot be saved', async () => {
    // Arrange
    mockLayout.saveFails = true
    render(<HomePage />)
    openCustomize()
    fireEvent.click(screen.getByRole('switch', { name: 'Show Personal' }))

    // Act
    await closeCustomize()

    // Assert
    expect(mockMessage.error).toHaveBeenCalledWith(
      expect.stringMatching(/could not be saved/),
    )
  })
})
