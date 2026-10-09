// The form lists and formats real days; the global mock only stubs format.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
import SprintTeamDaysOffForm, {
  sprintPlannedDays,
} from './sprint-team-days-off-form'

const setTeamDaysOff = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useSetSprintTeamDaysOffMutation: () => [setTeamDaysOff, { isLoading: false }],
}))

const sprint: SprintDetailsDto = {
  id: 'sprint-1',
  key: 201,
  name: '26.3.2',
  state: { id: 1, name: 'Future' },
  start: '2026-08-31',
  end: '2026-09-04',
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  teamDaysOff: ['2026-09-02'],
  canManageSprint: true,
  canStart: false,
  canComplete: false,
  canReopen: false,
  sprintType: SprintType.Standard,
  sprintTypeSource: SprintTypeSource.Default,
}

describe('sprintPlannedDays', () => {
  it('lists every planned day, first to last', () => {
    // Act
    const days = sprintPlannedDays(sprint)

    // Assert
    expect(days).toEqual([
      '2026-08-31',
      '2026-09-01',
      '2026-09-02',
      '2026-09-03',
      '2026-09-04',
    ])
  })
})

describe('SprintTeamDaysOffForm', () => {
  beforeEach(() => jest.clearAllMocks())

  it('starts from the sprint’s days off and saves the chosen days in date order', async () => {
    // Arrange
    setTeamDaysOff.mockResolvedValue({ data: undefined })
    const onFormComplete = jest.fn()
    render(
      <SprintTeamDaysOffForm
        sprint={sprint}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    )
    expect(
      screen.getByRole('checkbox', { name: 'Wed, Sep 2, 2026' }),
    ).toBeChecked()

    // Act
    await userEvent.click(
      screen.getByRole('checkbox', { name: 'Mon, Aug 31, 2026' }),
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(setTeamDaysOff).toHaveBeenCalledWith({
      id: 'sprint-1',
      key: 201,
      teamDaysOff: ['2026-08-31', '2026-09-02'],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('reports a refusal and stays open', async () => {
    // Arrange
    setTeamDaysOff.mockResolvedValue({
      error: {
        status: 400,
        detail:
          'Only members of the sprint’s team can change its team days off.',
      },
    })
    const onFormComplete = jest.fn()
    render(
      <SprintTeamDaysOffForm
        sprint={sprint}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(errorMessage).toHaveBeenCalledWith(
      'Only members of the sprint’s team can change its team days off.',
    )
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
