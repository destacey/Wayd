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
  /** When set, a save stays in flight until this settles. */
  saveHeld: null as Promise<null> | null,
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
            : (mockLayout.saveHeld ?? Promise.resolve(null)),
      }
    },
  ],
}))

/** The state each stand-in widget reports, by its label; 'rows' when unset. */
let mockWidgetStates: Record<
  string,
  import('./_components/home/widget-display').WidgetState
> = {}

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
vi.mock(import('antd'), async (importOriginal) => {
  const actual = await importOriginal()
  const MockDrawer = ({ open, footer, children }: any) =>
    open ? (
      <div data-testid="customize-drawer">
        {children}
        {footer}
      </div>
    ) : null
  MockDrawer.displayName = 'MockDrawer'
  // Stubs the component only; nothing here renders antd's static inner panel.
  return { ...actual, Drawer: MockDrawer as unknown as typeof actual.Drawer }
})

// Real gating and layout over stand-in widgets, so the page test sees what the
// registry decides without each widget's requests. Each stand-in shows the
// title the page hands it.
vi.mock('./_components/home/home-widgets', async () => {
  const { lazy, useContext } =
    await vi.importActual<typeof import('react')>('react')
  const { WidgetSettingsContext } = await vi.importActual<
    typeof import('./_components/home/widget-settings-form')
  >('./_components/home/widget-settings-form')
  const { useWidgetState } = await vi.importActual<
    typeof import('./_components/home/widget-display')
  >('./_components/home/widget-display')
  const { emptySetting } = await vi.importActual<
    typeof import('./_components/home/widget-settings')
  >('./_components/home/widget-settings')
  const stand = (label: string) => {
    const Stand = ({
      title,
      settings,
    }: {
      title?: string
      settings?: Record<string, string>
    }) => {
      // Stands in for the gear a real widget's card offers.
      const access = useContext(WidgetSettingsContext)
      // Reports as a real widget's card does.
      useWidgetState(mockWidgetStates[label] ?? 'rows')
      return (
        <div>
          <span data-rows={settings?.rows}>
            {label}
            {title ? ` (${title})` : ''}
          </span>
          {access?.widget.settings ? (
            <button onClick={() => access.onChange({ rows: '10' })}>
              Set {title} rows
            </button>
          ) : null}
        </div>
      )
    }
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
        settings: [
          {
            kind: 'choice',
            key: 'rows',
            label: 'Rows to show',
            options: [
              { value: '5', label: '5' },
              { value: '10', label: '10' },
            ],
            default: '5',
          },
          emptySetting,
        ],
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
        settings: [emptySetting],
      },
      {
        id: 'permitted',
        title: 'Permitted',
        description: '',
        defaultColumn: 'main',
        defaultOrder: 20,
        requires: { permissions: ['Permissions.PlanningIntervals.View'] },
        Component: stand('Permitted widget'),
        settings: [emptySetting],
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
    mockLayout.saveHeld = null
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
    settings: Record<string, string> = {},
  ): HomeLayoutWidgetResponse => ({ widgetId, column, visible, settings })

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
    mockLayout.saveHeld = null
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

  it('hands a widget its settings: the default, or what the user saved', () => {
    // Arrange
    const { unmount } = render(<HomePage />)
    const rows = () =>
      screen.getByText(/Personal widget/).getAttribute('data-rows')
    expect(rows()).toBe('5')
    unmount()
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main, true, { rows: '10' }),
    ]

    // Act
    render(<HomePage />)

    // Assert
    expect(rows()).toBe('10')
  })

  it('saves a setting changed on a card at once, as that widget alone', async () => {
    // Arrange
    render(<HomePage />)

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Set Personal rows' }))
    })

    // Assert
    expect(mockUpdateHomeLayout).toHaveBeenCalledWith({
      widgets: [saved('personal', HomeLayoutColumn.Main, true, { rows: '10' })],
    })
  })

  it('keeps showing a card change while its save is in flight', async () => {
    // Arrange — the stand-in cache is never patched, so only the layout being sent can show the change
    let finishSave: (value: null) => void = () => {}
    mockLayout.saveHeld = new Promise<null>((resolve) => {
      finishSave = resolve
    })
    render(<HomePage />)
    const rows = () =>
      screen.getByText(/Personal widget/).getAttribute('data-rows')

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Set Personal rows' }))
    })

    // Assert
    expect(rows()).toBe('10')

    // Act
    await act(async () => {
      finishSave(null)
    })

    // Assert — back to what the cache holds once nothing is in flight
    expect(rows()).toBe('5')
  })

  it('says a card change could not be saved, without pointing at Customize', async () => {
    // Arrange
    mockLayout.saveFails = true
    render(<HomePage />)

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Set Personal rows' }))
    })

    // Assert
    expect(mockMessage.error).toHaveBeenCalledTimes(1)
    expect(mockMessage.error).toHaveBeenCalledWith(
      expect.stringMatching(/change to this widget could not be saved/),
    )
    expect(mockMessage.error).not.toHaveBeenCalledWith(
      expect.stringMatching(/Customize/),
    )
  })

  it('saves a setting changed in the drawer when it closes', async () => {
    // Arrange
    render(<HomePage />)
    openCustomize()
    fireEvent.click(screen.getByRole('button', { name: 'Personal settings' }))

    // Act
    fireEvent.click(screen.getByRole('radio', { name: '10' }))

    // Assert — the page follows at once; nothing is saved yet
    expect(screen.getByText(/Personal widget/).getAttribute('data-rows')).toBe(
      '10',
    )
    expect(mockUpdateHomeLayout).not.toHaveBeenCalled()

    // Act
    await closeCustomize()

    // Assert
    expect(mockUpdateHomeLayout).toHaveBeenCalledWith({
      widgets: [
        saved('personal', HomeLayoutColumn.Main, true, { rows: '10' }),
        saved('flagged', HomeLayoutColumn.Side),
      ],
    })
  })

  it('offers no settings on a card while the layout cannot be saved', () => {
    // Arrange
    mockLayout.loadFails = true

    // Act
    render(<HomePage />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Set Personal rows' }),
    ).not.toBeInTheDocument()
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

describe('HomePage empty widgets', () => {
  const saved = (
    widgetId: string,
    column: HomeLayoutColumn,
    settings: Record<string, string> = {},
  ): HomeLayoutWidgetResponse => ({ widgetId, column, visible: true, settings })

  const sectionOf = (text: string) => screen.getByText(text).closest('section')

  const hiddenLine = () => screen.queryByText(/nothing to show:/)

  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    mockAuth.claims = new Set(['Permissions.Projects.View'])
    mockAuth.flags = new Set(['following'])
    mockAuth.flagsLoading = false
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main, { empty: 'hide' }),
      saved('flagged', HomeLayoutColumn.Side),
    ]
    mockLayout.isLoading = false
    mockLayout.saveFails = false
    mockLayout.saveHeld = null
    mockLayout.loadFails = false
    mockHome.context = undefined
    mockHome.isLoading = false
    mockWidgetStates = {}
  })

  afterAll(() => {
    mockWidgetStates = {}
  })

  it('hides an empty widget set to Hide and names it at the foot of the page', () => {
    // Arrange
    mockWidgetStates = { 'Personal widget': 'empty' }

    // Act
    render(<HomePage />)

    // Assert
    expect(sectionOf('Personal widget (Personal)')).not.toBeVisible()
    expect(sectionOf('Flagged widget (Flagged)')).toBeVisible()
    expect(hiddenLine()).toHaveTextContent(
      '1 widget has nothing to show: Personal.',
    )
  })

  it.each(['loading', 'failed', 'rows'] as const)(
    'shows a widget set to Hide while it is %s',
    (state) => {
      // Arrange
      mockWidgetStates = { 'Personal widget': state }

      // Act
      render(<HomePage />)

      // Assert
      expect(sectionOf('Personal widget (Personal)')).toBeVisible()
      expect(hiddenLine()).not.toBeInTheDocument()
    },
  )

  it('shows an empty widget left on Explain', () => {
    // Arrange
    mockWidgetStates = { 'Flagged widget': 'empty' }

    // Act
    render(<HomePage />)

    // Assert
    expect(sectionOf('Flagged widget (Flagged)')).toBeVisible()
    expect(hiddenLine()).not.toBeInTheDocument()
  })

  it('keeps a hidden widget mounted, so it comes back when it fills', () => {
    // Arrange
    mockWidgetStates = { 'Personal widget': 'empty' }
    const { rerender } = render(<HomePage />)

    // Act
    mockWidgetStates = { 'Personal widget': 'rows' }
    rerender(<HomePage />)

    // Assert
    expect(sectionOf('Personal widget (Personal)')).toBeVisible()
    expect(hiddenLine()).not.toBeInTheDocument()
  })

  it('does not name a widget drawn as one line', () => {
    // Arrange
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main, { empty: 'line' }),
      saved('flagged', HomeLayoutColumn.Side),
    ]
    mockWidgetStates = { 'Personal widget': 'empty' }

    // Act
    render(<HomePage />)

    // Assert
    expect(sectionOf('Personal widget (Personal)')).toBeVisible()
    expect(hiddenLine()).not.toBeInTheDocument()
  })

  it('names three or more widgets as a list', () => {
    // Arrange
    mockAuth.claims = new Set([
      'Permissions.Projects.View',
      'Permissions.PlanningIntervals.View',
    ])
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main, { empty: 'hide' }),
      saved('permitted', HomeLayoutColumn.Main, { empty: 'hide' }),
      saved('flagged', HomeLayoutColumn.Side, { empty: 'hide' }),
    ]
    mockWidgetStates = {
      'Personal widget': 'empty',
      'Permitted widget': 'empty',
      'Flagged widget': 'empty',
    }

    // Act
    render(<HomePage />)

    // Assert
    expect(hiddenLine()).toHaveTextContent(
      '3 widgets have nothing to show: Personal, Permitted and Flagged.',
    )
  })

  it('gives the side column the full width when every main widget is hidden', () => {
    // Arrange
    mockWidgetStates = { 'Personal widget': 'empty' }

    // Act
    render(<HomePage />)

    // Assert: the wrapper drops its two-column grid.
    const columns = sectionOf('Flagged widget (Flagged)')?.parentElement
      ?.parentElement
    expect(columns?.className).toBe('')
  })

  it('shows the hidden widgets on Show, and hides them again on Hide', () => {
    // Arrange
    mockWidgetStates = { 'Personal widget': 'empty' }
    render(<HomePage />)

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Show the widgets with nothing to show',
      }),
    )

    // Assert: still named, so the user can hide them again.
    expect(sectionOf('Personal widget (Personal)')).toBeVisible()
    expect(hiddenLine()).toBeInTheDocument()

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Hide the widgets with nothing to show',
      }),
    )

    // Assert
    expect(sectionOf('Personal widget (Personal)')).not.toBeVisible()
  })

  it('is the line alone when every widget is hidden as empty', () => {
    // Arrange
    mockLayout.widgets = [
      saved('personal', HomeLayoutColumn.Main, { empty: 'hide' }),
      saved('flagged', HomeLayoutColumn.Side, { empty: 'hide' }),
    ]
    mockWidgetStates = {
      'Personal widget': 'empty',
      'Flagged widget': 'empty',
    }

    // Act
    render(<HomePage />)

    // Assert
    expect(sectionOf('Personal widget (Personal)')).not.toBeVisible()
    expect(sectionOf('Flagged widget (Flagged)')).not.toBeVisible()
    expect(hiddenLine()).toHaveTextContent(
      '2 widgets have nothing to show: Personal and Flagged.',
    )
    expect(screen.queryByText(/Every widget is switched off/)).toBeNull()
  })
})
