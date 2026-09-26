import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { SchedulingSettingsDto, TimeZoneDto } from '@/src/services/wayd-api'
import SchedulingSettingsForm, {
  SchedulingSettingsFormProps,
} from './scheduling-settings-form'

const mockUpdate = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateSchedulingSettingsMutation: () => [mockUpdate, { isLoading: false }],
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: SchedulingSettingsDto = {
  defaultTimeZone: 'America/Chicago',
  defaultCommitmentGraceDays: 1,
}

const timeZones: TimeZoneDto[] = [
  { id: 'America/Chicago', currentOffset: '-05:00' },
  { id: 'UTC', currentOffset: '+00:00' },
]

const renderForm = (overrides: Partial<SchedulingSettingsFormProps> = {}) =>
  render(
    <SchedulingSettingsForm
      settings={settings}
      timeZones={timeZones}
      isLoading={false}
      canUpdate={true}
      {...overrides}
    />,
  )

const graceDaysInput = () =>
  screen.getByRole('spinbutton', {
    name: 'Default commitment grace period (days)',
  })

describe('SchedulingSettingsForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows the saved values', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('(UTC-05:00) America/Chicago')).toBeInTheDocument()
    expect(graceDaysInput()).toHaveValue('1')
  })

  it('keeps Save disabled until a value changes', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('saves the edited values', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act
    fireEvent.change(graceDaysInput(), { target: { value: '3' } })
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({
        defaultTimeZone: 'America/Chicago',
        defaultCommitmentGraceDays: 3,
      }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('reports a validation rejection as one', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        status: 422,
        errors: {
          DefaultTimeZone: ["'Mars/Base' is not a valid IANA time zone."],
        },
      },
    })
    renderForm()

    // Act
    fireEvent.change(graceDaysInput(), { target: { value: '2' } })
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
    expect(graceDaysInput()).toBeDisabled()
  })
})
