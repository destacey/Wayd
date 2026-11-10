import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DependenciesAtRiskTile } from './my-teams-tile'
import {
  HomeContextResponse,
  MyTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import { homeContext } from '../home-context-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyTeamsSummaryQuery: vi.fn(),
}))

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'

const mockSummary = useGetMyTeamsSummaryQuery as Mock

const loaded = (summary: Partial<MyTeamsSummaryResponse>) => ({
  data: { teams: [], dependenciesAtRiskCount: 0, ...summary },
  isLoading: false,
  isError: false,
})

describe('DependenciesAtRiskTile', () => {
  beforeEach(() => {
    mockHome.context = homeContext({ teams: { teamCount: 2 } })
  })

  it('counts the dependencies at risk across the teams', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ dependenciesAtRiskCount: 3 }))

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 3',
      }),
    ).toBeInTheDocument()
  })

  it('reads zero when the caller may not see dependencies', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded({ dependenciesAtRiskCount: undefined }))

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 0',
      }),
    ).toBeInTheDocument()
  })

  it('reads zero, without asking, for someone on no team', () => {
    // Arrange
    mockHome.context = homeContext()
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: 0',
      }),
    ).toBeInTheDocument()
    expect(mockSummary).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('shows a dash when the summary could not be loaded', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<DependenciesAtRiskTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Dependencies at risk on your teams: unavailable',
      }),
    ).toBeInTheDocument()
  })
})
