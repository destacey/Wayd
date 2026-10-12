import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { SprintMetricsSettingsDto } from '@/src/services/wayd-api'
import SprintMetricsSettingsForm, {
  SprintMetricsSettingsFormProps,
} from './sprint-metrics-settings-form'

const mockUpdate = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateSprintMetricsSettingsMutation: () => [
    mockUpdate,
    { isLoading: false },
  ],
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: SprintMetricsSettingsDto = { sprintWindow: 7 }

const renderForm = (overrides: Partial<SprintMetricsSettingsFormProps> = {}) =>
  render(
    <SprintMetricsSettingsForm
      settings={settings}
      isLoading={false}
      canUpdate={true}
      {...overrides}
    />,
  )

const sprintWindowInput = () =>
  screen.getByRole('spinbutton', { name: 'Sprint window (sprints)' })

describe('SprintMetricsSettingsForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows the saved value', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(sprintWindowInput()).toHaveValue('7')
  })

  it('keeps Save disabled until the value changes', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('saves the edited value', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act
    fireEvent.change(sprintWindowInput(), { target: { value: '5' } })
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({ sprintWindow: 5 }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('reports a validation rejection as one', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        status: 422,
        errors: { SprintWindow: ["'Sprint Window' must be between 3 and 12."] },
      },
    })
    renderForm()

    // Act
    fireEvent.change(sprintWindowInput(), { target: { value: '5' } })
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockError).toHaveBeenCalledWith(
        'Correct the validation error(s) to continue.',
      ),
    )
    expect(mockSuccess).not.toHaveBeenCalled()
  })

  it('is read-only without the update permission', () => {
    // Arrange / Act
    renderForm({ canUpdate: false })

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Save' }),
    ).not.toBeInTheDocument()
    expect(sprintWindowInput()).toBeDisabled()
  })
})
