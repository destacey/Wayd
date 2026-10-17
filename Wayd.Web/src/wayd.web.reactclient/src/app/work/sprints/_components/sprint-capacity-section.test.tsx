import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
import SprintCapacitySection from './sprint-capacity-section'

const mockPlan = jest.fn()
jest.mock(
  '@/src/app/organizations/_components/capacity-plan/team-capacity-plan',
  () => ({
    __esModule: true,
    default: (props: { extraActions?: React.ReactNode }) => {
      mockPlan(props)
      return <div>{props.extraActions}</div>
    },
  }),
)
jest.mock('./sprint-capacity-summary', () => ({
  __esModule: true,
  default: () => <div />,
}))

const sprint = (teamIsActive: boolean): SprintDetailsDto => ({
  id: 'sprint-1',
  key: 201,
  name: '26.3.2',
  state: { id: 1, name: 'Future' },
  start: '2026-08-17',
  end: '2026-08-30',
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  teamIsActive,
  holidays: [],
  workingDays: [],
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  teamDaysOff: [],
  workingDayHours: 8,
  canManageSprint: false,
  canStart: false,
  canComplete: false,
  canReopen: false,
  sprintType: SprintType.Standard,
  sprintTypeSource: SprintTypeSource.Default,
})

describe('SprintCapacitySection', () => {
  it.each([true, false])(
    'plans the team over the sprint’s dates, active: %s',
    (teamIsActive) => {
      // Arrange / Act
      render(<SprintCapacitySection sprint={sprint(teamIsActive)} />)

      // Assert
      expect(mockPlan).toHaveBeenLastCalledWith({
        team: { id: 't1', name: 'Team Atlas', isActive: teamIsActive },
        teamType: 'Team',
        from: '2026-08-17',
        to: '2026-08-30',
        daysOff: [],
      })
      expect(
        screen.queryByRole('button', { name: 'Team Days Off' }),
      ).not.toBeInTheDocument()
    },
  )

  it('offers the team days off beside the plan to those who can change them', async () => {
    // Arrange
    const onSetTeamDaysOff = jest.fn()
    const withHalfDay = {
      ...sprint(true),
      teamDaysOff: [{ date: '2026-08-19', percentOff: 50 }],
    }
    render(
      <SprintCapacitySection
        sprint={withHalfDay}
        onSetTeamDaysOff={onSetTeamDaysOff}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Team Days Off' }))

    // Assert
    expect(onSetTeamDaysOff).toHaveBeenCalled()
    expect(mockPlan).toHaveBeenLastCalledWith(
      expect.objectContaining({
        daysOff: [{ date: '2026-08-19', percentOff: 50 }],
      }),
    )
  })
})
