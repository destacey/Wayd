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
// we're testing here (which tab is active for a given URL hash). Stub it.
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

const setHash = (hash: string) => {
  window.history.replaceState(
    null,
    '',
    `${window.location.pathname}${window.location.search}${hash}`,
  )
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
    setHash('')
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

  it('falls back to the first team when no hash is present', async () => {
    await renderPage()

    expect(await screen.findByTestId('team-plan-review-stub')).toHaveTextContent(
      'team:CORE',
    )
  })

  it('honors the URL hash and selects the matching team on first render', async () => {
    setHash('#data')

    await renderPage()

    expect(await screen.findByTestId('team-plan-review-stub')).toHaveTextContent(
      'team:DATA',
    )
    // And critically, doesn't rewrite the hash to the first team.
    expect(window.location.hash).toBe('#data')
  })

  it('does not append a second hash segment when navigating in via a hash link', async () => {
    setHash('#data')

    await renderPage()

    // After the page settles, the URL should have exactly one fragment.
    expect(window.location.hash).toBe('#data')
    expect(window.location.href).not.toMatch(/#.*#/)
  })

  // Regression: the page used to read window.location.hash during render and
  // mirror activeTab to the URL via an effect. In Next.js's client router,
  // the destination page renders BEFORE window.location.hash reflects the
  // incoming hash — so the render-phase read returned "", the page fell back
  // to the first team alphabetically, and the mirror effect overwrote the
  // user-supplied hash with that fallback. The fix reads the hash in an
  // effect (post-commit) and gates the mirror effect on that read, so the
  // URL is never rewritten before we know the user's intent.
  //
  // This regression test asserts the page's *behavior* — that it does not
  // clobber the URL or render the wrong team when arriving with a hash —
  // rather than the buggy implementation detail. A stronger end-to-end
  // assertion of the original race would require Playwright; jsdom always
  // exposes window.location.hash synchronously and so can't faithfully
  // simulate it.
  it('honors the hash even when teams are already cached on first render', async () => {
    // Cached state — both queries return data synchronously, mirroring the
    // common flow of arriving from the overview where teamsQuery is warm.
    setHash('#data')

    await renderPage()

    expect(
      await screen.findByTestId('team-plan-review-stub'),
    ).toHaveTextContent('team:DATA')
    // Critically: the URL hash is still what the user clicked, not the
    // first-team fallback ("#core").
    expect(window.location.hash).toBe('#data')
  })

  it('reflects manual hashchange events into the active team', async () => {
    setHash('#core')

    await renderPage()

    expect(await screen.findByTestId('team-plan-review-stub')).toHaveTextContent(
      'team:CORE',
    )

    setHash('#data')
    await act(async () => {
      window.dispatchEvent(new HashChangeEvent('hashchange'))
    })

    expect(await screen.findByTestId('team-plan-review-stub')).toHaveTextContent(
      'team:DATA',
    )
  })
})
