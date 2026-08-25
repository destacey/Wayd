import { act, render, screen } from '@testing-library/react'
import { Suspense } from 'react'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(),
    removeListener: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
})

let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn((url: string) => {
  mockSearchParams = new URLSearchParams(url.split('?')[1] ?? '')
})

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/planning/planning-intervals/7/plan-review',
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
  useSearchParams: () => mockSearchParams,
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: () => true,
    hasPermissionClaim: () => true,
  }),
}))

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalQuery: jest.fn(),
  useGetPlanningIntervalTeamsQuery: jest.fn(),
}))

// TeamPlanReview pulls many sibling RTK queries that aren't relevant to what
// we're testing here (which team is active for a given URL). Stub it.
jest.mock('./team-plan-review', () => ({
  __esModule: true,
  default: ({ team }: { team: { code: string } | null }) => (
    <div data-testid="team-plan-review-stub">
      {team ? `team:${team.code}` : 'no-team'}
    </div>
  ),
}))

import {
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import PlanningIntervalPlanReviewPage from './page'

const mockPiQuery = useGetPlanningIntervalQuery as unknown as jest.Mock
const mockTeamsQuery = useGetPlanningIntervalTeamsQuery as unknown as jest.Mock

const setTeamParam = (code?: string) => {
  mockSearchParams = new URLSearchParams(code ? `team=${code}` : '')
}

// react's `use()` suspends on a Promise even if it's already resolved at
// construction time. To avoid Suspense churn in unit tests, we hand the page
// a thenable that calls back synchronously — `use()` treats it as resolved
// on the very first render. This is a documented escape hatch in React docs.
const syncResolvedParams = <T,>(value: T): Promise<T> => {
  const p: any = { then: (resolve: (v: T) => void) => resolve(value) }
  return p
}

const renderPage = async () => {
  await act(async () => {
    render(
      <Suspense fallback={<div data-testid="suspense-fallback" />}>
        <PlanningIntervalPlanReviewPage
          params={syncResolvedParams({ key: '7' })}
        />
      </Suspense>,
    )
  })
}

describe('PlanningIntervalPlanReviewPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    setTeamParam()
    mockPiQuery.mockReturnValue({
      data: { id: 'pi-1', key: 7, name: '2026 PI 1', predictability: 50 },
      isLoading: false,
      refetch: jest.fn(),
    })
    mockTeamsQuery.mockReturnValue({
      data: [
        // sorted by code in the page; we deliberately seed unsorted so the
        // first-team fallback is unambiguous (DATA, the alphabetic first).
        { id: 't2', key: 2, name: 'Engineering', code: 'CORE', type: 'Team' },
        { id: 't1', key: 1, name: 'Analytics', code: 'DATA', type: 'Team' },
      ],
      isLoading: false,
    })
  })

  it('falls back to the first team when the URL names none', () => {
    // Arrange / Act — teams are seeded unsorted, so CORE winning proves the
    // page sorted by code rather than taking whatever arrived first.
    return renderPage().then(async () => {
      // Assert
      expect(
        await screen.findByTestId('team-plan-review-stub'),
      ).toHaveTextContent('team:CORE')
    })
  })

  it('shows the team the URL names on the first paint', async () => {
    // Arrange — a search param is readable during render, unlike a hash, so
    // there is no frame showing the wrong team before it corrects itself.
    setTeamParam('data')

    // Act
    await renderPage()

    // Assert
    expect(await screen.findByTestId('team-plan-review-stub')).toHaveTextContent(
      'team:DATA',
    )
  })

  it('does not rewrite the URL when arriving with a team already named', async () => {
    // Arrange — the page used to overwrite the incoming selection with its
    // first-team fallback, because it could not read the hash during render.
    setTeamParam('data')

    // Act
    await renderPage()

    // Assert
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('names the chosen team in the URL rather than in component state', async () => {
    // Arrange
    setTeamParam('core')
    await renderPage()

    // Act
    await act(async () => {
      screen.getByRole('tab', { name: 'DATA' }).click()
    })

    // Assert — replace, not push: Back returns where the user came from
    // rather than stepping through every team they looked at.
    expect(mockReplace).toHaveBeenCalledWith(
      '/planning/planning-intervals/7/plan-review?team=data',
      { scroll: false },
    )
  })

  it('warns rather than guessing when the URL names a team the PI does not have', async () => {
    // Arrange — a stale link, or a team since removed from the PI.
    setTeamParam('gone')

    // Act
    await renderPage()

    // Assert
    expect(await screen.findByText('Please select a valid team.')).toBeInTheDocument()
    expect(screen.queryByTestId('team-plan-review-stub')).toBeNull()
  })
})
