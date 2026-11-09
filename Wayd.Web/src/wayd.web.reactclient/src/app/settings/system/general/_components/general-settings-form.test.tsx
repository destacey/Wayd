import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { GeneralSettingsDto, TimeZoneDto } from '@/src/services/wayd-api'
import GeneralSettingsForm, {
  GeneralSettingsFormProps,
} from './general-settings-form'

const mockUpdate = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateGeneralSettingsMutation: () => [mockUpdate, { isLoading: false }],
}))

const timeZones: TimeZoneDto[] = [
  { id: 'America/Chicago', currentOffset: '-05:00' },
  { id: 'Europe/London', currentOffset: '+00:00' },
]

jest.mock('@/src/store/features/common/time-zones-api', () => ({
  useGetTimeZonesQuery: () => ({ data: timeZones, isLoading: false }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: GeneralSettingsDto = { timeZone: 'America/Chicago' }

const renderForm = (overrides: Partial<GeneralSettingsFormProps> = {}) =>
  render(
    <GeneralSettingsForm
      settings={settings}
      isLoading={false}
      canUpdate={true}
      {...overrides}
    />,
  )

const timeZoneSelect = () =>
  screen.getByRole('combobox', { name: 'System time zone' })

const chooseLondon = () => {
  fireEvent.mouseDown(timeZoneSelect())
  fireEvent.click(screen.getByTitle('(UTC+00:00) Europe/London'))
}

describe('GeneralSettingsForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows the saved time zone', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('(UTC-05:00) America/Chicago')).toBeInTheDocument()
  })

  it('keeps Save disabled until the time zone changes', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('saves the chosen time zone', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act
    chooseLondon()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({ timeZone: 'Europe/London' }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('reports a validation rejection on the time zone field', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        status: 422,
        errors: { TimeZone: ["'Mars/Base' is not a valid IANA time zone."] },
      },
    })
    renderForm()

    // Act
    chooseLondon()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockError).toHaveBeenCalledWith(
        'Correct the validation error(s) to continue.',
      ),
    )
    // antd shows a field's errors a timer tick after they are set, which a
    // loaded CI runner can push past findByText's default one second.
    expect(
      await screen.findByText(
        "'Mars/Base' is not a valid IANA time zone.",
        {},
        { timeout: 5000 },
      ),
    ).toBeInTheDocument()
    expect(mockSuccess).not.toHaveBeenCalled()
  })

  it("shows the server's message when the save is refused", async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        isApiException: true,
        status: 403,
        response: { detail: 'You do not have permission to do that.' },
      },
    })
    renderForm()

    // Act
    chooseLondon()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockError).toHaveBeenCalledWith(
        'You do not have permission to do that.',
      ),
    )
  })

  it('is read-only without the update permission', () => {
    // Arrange / Act
    renderForm({ canUpdate: false })

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Save' }),
    ).not.toBeInTheDocument()
    expect(timeZoneSelect()).toBeDisabled()
  })
})
