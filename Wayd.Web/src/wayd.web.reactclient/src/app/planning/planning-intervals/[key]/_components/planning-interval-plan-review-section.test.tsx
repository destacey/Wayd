import { act, render, screen } from '@testing-library/react'

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
const mockReplace = jest.fn()

jest.mock('next/navigation', () => ({
  usePathname: () => '/planning/planning-intervals/7',
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
  useGetPlanningIntervalTeamsQuery: jest.fn(),
}))

// TeamPlanReview pulls many sibling RTK queries that aren't relevant to what
// we're testing here (which team is active for a given URL). Stub it.
jest.mock('./plan-review/team-plan-review', () => ({
  __esModule: true,
  default: ({ team }: { team: { code: string } | null }) => (
    <div data-testid="team-plan-review-stub">
      {team ? `team:${team.code}` : 'no-team'}
    </div>
  ),
}))

import { useGetPlanningIntervalTeamsQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalDetailsDto } from '@/src/services/wayd-api'
import PlanningIntervalPlanReviewSection from './planning-interval-plan-review-section'

const mockTeamsQuery = useGetPlanningIntervalTeamsQuery as unknown as jest.Mock

const planningInterval = {
  id: 'pi-1',
  key: 7,
  name: '2026 PI 1',
} as PlanningIntervalDetailsDto

const setTeamParam = (code?: string) => {
  mockSearchParams = new URLSearchParams(code ? `team=${code}` : '')
}

const renderSection = async () => {
  await act(async () => {
    render(
      <PlanningIntervalPlanReviewSection
        planningInterval={planningInterval}
        refreshPlanningInterval={jest.fn()}
      />,
    )
  })
}

describe('PlanningIntervalPlanReviewSection', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    setTeamParam()
    mockTeamsQuery.mockReturnValue({
      data: [
        // DATA arrives first so the fallback landing on CORE proves the
        // section sorted by code rather than taking whatever came back first.
        { id: 't1', key: 1, name: 'Analytics', code: 'DATA', type: 'Team' },
        { id: 't2', key: 2, name: 'Engineering', code: 'CORE', type: 'Team' },
      ],
      isLoading: false,
    })
  })

  it('falls back to the first team when the URL names none', async () => {
    // Arrange / Act
    await renderSection()

    // Assert
    expect(
      await screen.findByTestId('team-plan-review-stub'),
    ).toHaveTextContent('team:CORE')
  })

  it('shows the team the URL names on the first paint', async () => {
    // Arrange — a search param is readable during render, unlike a hash, so
    // there is no frame showing the wrong team before it corrects itself.
    setTeamParam('data')

    // Act
    await renderSection()

    // Assert
    expect(
      await screen.findByTestId('team-plan-review-stub'),
    ).toHaveTextContent('team:DATA')
  })

  it('does not rewrite the URL when arriving with a team already named', async () => {
    // Arrange
    setTeamParam('data')

    // Act
    await renderSection()

    // Assert
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('names the chosen team in the URL rather than in component state', async () => {
    // Arrange
    setTeamParam('core')
    await renderSection()

    // Act
    await act(async () => {
      screen.getByRole('tab', { name: 'DATA' }).click()
    })

    // Assert — replace, not push: Back returns where the user came from
    // rather than stepping through every team they looked at.
    expect(mockReplace).toHaveBeenCalledWith(
      '/planning/planning-intervals/7?team=data',
      { scroll: false },
    )
  })

  it('keeps the section in the URL when switching teams', async () => {
    // Arrange — the team tab must not drop the param that put the record on
    // this section, or switching teams would bounce back to Overview.
    mockSearchParams = new URLSearchParams('section=plan-review&team=core')
    await renderSection()

    // Act
    await act(async () => {
      screen.getByRole('tab', { name: 'DATA' }).click()
    })

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/planning/planning-intervals/7?section=plan-review&team=data',
      { scroll: false },
    )
  })

  it('warns rather than guessing when the URL names a team the PI does not have', async () => {
    // Arrange — a stale link, or a team since removed from the PI.
    setTeamParam('gone')

    // Act
    await renderSection()

    // Assert
    expect(
      await screen.findByText('Please select a valid team.'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('team-plan-review-stub')).toBeNull()
  })
})
