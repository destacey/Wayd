import { HomeContextResponse } from '@/src/services/wayd-api'
import { act, render, screen } from '@testing-library/react'
import { homeContext } from '../home-context-test-utils'
import MyTeamSprintsWidget from './my-team-sprints-widget'

const mockHome: { context?: HomeContextResponse } = {}

jest.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

let mockReport: {
  hasSprints: (hasSprints: boolean) => void
  loadError: () => void
}

jest.mock(
  '@/src/components/common/planning/my-team-sprints',
  () =>
    function MockMyTeamSprints({
      onHasSprintsChange,
      onLoadError,
    }: {
      onHasSprintsChange: (hasSprints: boolean) => void
      onLoadError: () => void
    }) {
      mockReport = { hasSprints: onHasSprintsChange, loadError: onLoadError }
      return <div>Team sprint cards</div>
    },
)

describe('MyTeamSprintsWidget', () => {
  beforeEach(() => {
    mockHome.context = undefined
  })

  it('says how it fills, without looking for sprints, when the caller is on no team', () => {
    // Arrange
    mockHome.context = homeContext({ teams: { teamOfTeamsCount: 1 } })

    // Act
    render(<MyTeamSprintsWidget title="My team sprints" />)

    // Assert
    expect(
      screen.getByText(/You are not a member of a team/),
    ).toBeInTheDocument()
    expect(screen.queryByText('Team sprint cards')).not.toBeInTheDocument()
  })

  it('draws a skeleton until the sprints are known', () => {
    // Arrange / Act
    const { container } = render(
      <MyTeamSprintsWidget title="My team sprints" />,
    )

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(screen.queryByText(/None of your teams/)).not.toBeInTheDocument()
  })

  it('says how it fills when no team has an active sprint', () => {
    // Arrange
    const { container } = render(
      <MyTeamSprintsWidget title="My team sprints" />,
    )

    // Act
    act(() => mockReport.hasSprints(false))

    // Assert
    expect(
      screen.getByText(/None of your teams has an active sprint/),
    ).toBeInTheDocument()
    expect(container.querySelector('.ant-skeleton')).not.toBeInTheDocument()
  })

  it('leaves the sprints to the existing component once there are some', () => {
    // Arrange
    const { container } = render(
      <MyTeamSprintsWidget title="My team sprints" />,
    )

    // Act
    act(() => mockReport.hasSprints(true))

    // Assert
    expect(screen.getByText('Team sprint cards')).toBeInTheDocument()
    expect(screen.queryByText(/None of your teams/)).not.toBeInTheDocument()
    expect(container.querySelector('.ant-skeleton')).not.toBeInTheDocument()
  })

  it('says the teams failed to load rather than that there are no sprints', () => {
    // Arrange
    render(<MyTeamSprintsWidget title="My team sprints" />)

    // Act
    act(() => mockReport.loadError())

    // Assert
    expect(screen.getByText('Could not load your teams.')).toBeInTheDocument()
    expect(screen.queryByText(/None of your teams/)).not.toBeInTheDocument()
  })
})
