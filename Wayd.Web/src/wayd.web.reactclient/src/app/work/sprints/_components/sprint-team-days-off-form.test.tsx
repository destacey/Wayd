// The form lists and formats real days; the global mock only stubs format.
jest.unmock('dayjs')

import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IsoDayOfWeek,
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
import SprintTeamDaysOffForm, {
  alreadyOff,
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
  teamDaysOff: [{ date: '2026-09-02', percentOff: 100 }],
  workingDayHours: 8,
  teamIsActive: true,
  holidays: [],
  workingDays: [],
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
      teamDaysOff: [
        { date: '2026-08-31', percentOff: 100 },
        { date: '2026-09-02', percentOff: 100 },
      ],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('offers no part days without allowPartDays, and keeps a part day already set', async () => {
    // Arrange
    setTeamDaysOff.mockResolvedValue({ data: undefined })
    render(
      <SprintTeamDaysOffForm
        sprint={{
          ...sprint,
          teamDaysOff: [{ date: '2026-09-02', percentOff: 25 }],
        }}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )
    expect(screen.queryByText('Half day')).not.toBeInTheDocument()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(setTeamDaysOff).toHaveBeenCalledWith(
      expect.objectContaining({
        teamDaysOff: [{ date: '2026-09-02', percentOff: 25 }],
      }),
    )
  })

  it.each([false, true])(
    'saves a stored day too small for a quarter hour unchanged, part days allowed: %p',
    async (allowPartDays) => {
      // Arrange — 1% of an 8-hour day is under five minutes
      setTeamDaysOff.mockResolvedValue({ data: undefined })
      render(
        <SprintTeamDaysOffForm
          sprint={{
            ...sprint,
            teamDaysOff: [{ date: '2026-09-02', percentOff: 1 }],
          }}
          allowPartDays={allowPartDays}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      )
      const save = screen.getByRole('button', { name: 'Save' })
      expect(save).toBeEnabled()

      // Act
      await userEvent.click(save)

      // Assert
      expect(setTeamDaysOff).toHaveBeenCalledWith(
        expect.objectContaining({
          teamDaysOff: [{ date: '2026-09-02', percentOff: 1 }],
        }),
      )
    },
  )

  it('saves a half day and hours of a day as the share of the working day', async () => {
    // Arrange
    setTeamDaysOff.mockResolvedValue({ data: undefined })
    render(
      <SprintTeamDaysOffForm
        sprint={sprint}
        allowPartDays
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )
    await userEvent.click(
      screen.getByRole('checkbox', { name: 'Thu, Sep 3, 2026' }),
    )

    // Act — half of the 2nd, and 2 of the 3rd's 8 hours
    const wednesday = screen.getByRole('radiogroup', {
      name: 'How much of Wed, Sep 2, 2026 the team is off',
    })
    await userEvent.click(within(wednesday).getByText('Half day'))
    const thursday = screen.getByRole('radiogroup', {
      name: 'How much of Thu, Sep 3, 2026 the team is off',
    })
    await userEvent.click(within(thursday).getByText('Hours'))
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    await userEvent.type(
      screen.getByRole('spinbutton', {
        name: 'Hours the team is off on Thu, Sep 3, 2026',
      }),
      '2',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(setTeamDaysOff).toHaveBeenCalledWith(
      expect.objectContaining({
        teamDaysOff: [
          { date: '2026-09-02', percentOff: 50 },
          { date: '2026-09-03', percentOff: 25 },
        ],
      }),
    )
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

describe('alreadyOff', () => {
  // Monday 31 August to Friday 4 September, worked Monday to Thursday, with
  // Monday a holiday.
  const withCalendar: SprintDetailsDto = {
    ...sprint,
    holidays: ['2026-08-31'],
    workingDays: [
      IsoDayOfWeek.Monday,
      IsoDayOfWeek.Tuesday,
      IsoDayOfWeek.Wednesday,
      IsoDayOfWeek.Thursday,
    ],
  }

  it.each([
    ['2026-08-31', 'Holiday'],
    ['2026-09-04', 'Not a working day'],
    ['2026-09-01', undefined],
  ])('says %s is %p', (day, expected) => {
    // Act
    const reason = alreadyOff(withCalendar, day)

    // Assert
    expect(reason).toBe(expected)
  })

  it('shows a holiday and a non-working day without letting them be chosen', async () => {
    // Arrange
    setTeamDaysOff.mockResolvedValue({ data: undefined })
    render(
      <SprintTeamDaysOffForm
        sprint={{ ...withCalendar, teamDaysOff: [] }}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )

    // Act
    const holiday = screen.getByRole('checkbox', { name: /Mon, Aug 31, 2026/ })
    const friday = screen.getByRole('checkbox', { name: /Fri, Sep 4, 2026/ })

    // Assert
    expect(holiday).toBeDisabled()
    expect(friday).toBeDisabled()
    expect(screen.getByText(/· Holiday/)).toBeInTheDocument()
    expect(screen.getByText(/· Not a working day/)).toBeInTheDocument()
    expect(
      screen.getByRole('checkbox', { name: 'Tue, Sep 1, 2026' }),
    ).toBeEnabled()
  })
})
