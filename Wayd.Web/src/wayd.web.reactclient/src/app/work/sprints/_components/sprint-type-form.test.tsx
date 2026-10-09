import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
import SprintTypeForm from './sprint-type-form'

const setSprintType = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useGetSprintTypesQuery: () => ({
    data: [
      { id: 1, code: 'Standard', name: 'Standard', order: 1 },
      { id: 2, code: 'NonStandard', name: 'Non-standard', order: 2 },
    ],
  }),
  useSetSprintTypeMutation: () => [setSprintType, { isLoading: false }],
}))

const sprint: SprintDetailsDto = {
  id: 'sprint-1',
  key: 201,
  name: '26.3.2',
  state: { id: 1, name: 'Future' },
  start: '2026-08-31',
  end: '2026-09-11',
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  teamDaysOff: [],
  sprintType: SprintType.NonStandard,
  sprintTypeSource: SprintTypeSource.PlanningInterval,
  canManageSprint: true,
  canStart: false,
  canComplete: false,
  canReopen: false,
}

const renderForm = (
  overrides: Partial<SprintDetailsDto> = {},
  onFormComplete = jest.fn(),
) =>
  render(
    <SprintTypeForm
      sprint={{ ...sprint, ...overrides }}
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

describe('SprintTypeForm', () => {
  beforeEach(() => jest.clearAllMocks())

  it('starts on following the planning interval when the team set no type', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(
      screen.getByRole('radio', { name: /Follow the planning interval/ }),
    ).toBeChecked()
  })

  it('starts on the type the team set', () => {
    // Arrange / Act
    renderForm({ sprintTypeSource: SprintTypeSource.Team })

    // Assert
    expect(screen.getByRole('radio', { name: /^Non-standard/ })).toBeChecked()
  })

  it('saves the chosen type', async () => {
    // Arrange
    setSprintType.mockResolvedValue({ data: undefined })
    const onFormComplete = jest.fn()
    renderForm({}, onFormComplete)

    // Act
    await userEvent.click(screen.getByRole('radio', { name: /^Standard/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(setSprintType).toHaveBeenCalledWith({
      id: 'sprint-1',
      key: 201,
      sprintType: SprintType.Standard,
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('clears the team’s type to follow the planning interval again', async () => {
    // Arrange
    setSprintType.mockResolvedValue({ data: undefined })
    renderForm({ sprintTypeSource: SprintTypeSource.Team })

    // Act
    await userEvent.click(
      screen.getByRole('radio', { name: /Follow the planning interval/ }),
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(setSprintType).toHaveBeenCalledWith({
      id: 'sprint-1',
      key: 201,
      sprintType: undefined,
    })
  })

  it('reports a refusal and stays open', async () => {
    // Arrange
    setSprintType.mockResolvedValue({
      error: {
        status: 400,
        detail: 'Only members of the sprint’s team can change its sprint type.',
      },
    })
    const onFormComplete = jest.fn()
    renderForm({}, onFormComplete)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(errorMessage).toHaveBeenCalledWith(
      'Only members of the sprint’s team can change its sprint type.',
    )
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
