import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { IsoDayOfWeek, SchedulingSettingsDto } from '@/src/services/wayd-api'
import SchedulingSettingsForm, {
  SchedulingSettingsFormProps,
} from './scheduling-settings-form'

const mockUpdate = vi.fn()
const mockSuccess = vi.fn()
const mockError = vi.fn()

vi.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateSchedulingSettingsMutation: () => [mockUpdate, { isLoading: false }],
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

vi.mock('@/src/store/features/organization/holiday-calendars-api', () => ({
  useGetHolidayCalendarsQuery: () => ({
    data: [
      {
        id: 'cal-us',
        key: 1,
        name: 'United States',
        holidayCount: 11,
        isDefault: false,
      },
    ],
    isLoading: false,
  }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: SchedulingSettingsDto = {
  defaultCommitmentGraceDays: 1,
  workingDayHours: 7.5,
  defaultWorkingDays: [
    IsoDayOfWeek.Monday,
    IsoDayOfWeek.Tuesday,
    IsoDayOfWeek.Wednesday,
    IsoDayOfWeek.Thursday,
    IsoDayOfWeek.Friday,
  ],
}

const renderForm = (overrides: Partial<SchedulingSettingsFormProps> = {}) =>
  render(
    <SchedulingSettingsForm
      settings={settings}
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
    vi.clearAllMocks()
  })

  it('shows the saved values', () => {
    // Arrange / Act
    renderForm()

    // Assert
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
        defaultCommitmentGraceDays: 3,
        defaultWorkingDays: settings.defaultWorkingDays,
        workingDayHours: 7.5,
      }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('saves a changed working week', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'Sun' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Fri' }))
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith(
        expect.objectContaining({
          defaultWorkingDays: [
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Sunday,
          ],
        }),
      ),
    )
  })

  it('reports a validation rejection as one', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        status: 422,
        errors: {
          DefaultCommitmentGraceDays: [
            "'Default Commitment Grace Days' must be between 0 and 14.",
          ],
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
