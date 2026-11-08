import { act, render, screen } from '@testing-library/react'
import HomePage from './page'

const mockAuth = {
  employeeId: 'emp-1' as string | null,
  claims: new Set<string>(),
  flags: new Set<string>(),
  flagsLoading: false,
}

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    user: { name: 'Alex Rivera', employeeId: mockAuth.employeeId },
    hasPermissionClaim: (claim: string) => mockAuth.claims.has(claim),
  }),
}))

jest.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
  useFeatureFlags: () => ({
    isEnabled: (name: string) => mockAuth.flags.has(name),
    isLoading: mockAuth.flagsLoading,
  }),
}))

jest.mock('@/src/hooks/use-document-title', () => ({
  useDocumentTitle: jest.fn(),
}))

// Real gating and layout over stand-in widgets, so the page test sees what the
// registry decides without each widget's requests. Each stand-in shows the
// title the page hands it.
jest.mock('./_components/home/home-widgets', () => {
  const { lazy } = jest.requireActual<typeof import('react')>('react')
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
      screen.getByText(/projects, risks, tasks and team sprints/),
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
