import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { App } from 'antd'
import {
  GeneralSettingsDto,
  TimeZoneDto,
} from '@/src/services/api/wayd.schemas'
import GeneralSettingsForm, {
  GeneralSettingsFormProps,
} from './general-settings-form'

const mockUpdate = vi.fn()
const mockSuccess = vi.fn()
const mockError = vi.fn()

vi.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateGeneralSettingsMutation: () => [mockUpdate, { isLoading: false }],
}))

const timeZones: TimeZoneDto[] = [
  { id: 'America/Chicago', currentOffset: '-05:00' },
  { id: 'Europe/London', currentOffset: '+00:00' },
]

vi.mock('@/src/store/features/common/time-zones-api', () => ({
  useGetTimeZonesQuery: () => ({ data: timeZones, isLoading: false }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: GeneralSettingsDto = { timeZone: 'America/Chicago' }

const renderForm = (overrides: Partial<GeneralSettingsFormProps> = {}) =>
  render(
    <App>
      <GeneralSettingsForm
        settings={settings}
        isLoading={false}
        canUpdate={true}
        {...overrides}
      />
    </App>,
  )

const timeZoneSelect = () =>
  screen.getByRole('combobox', { name: 'System time zone' })

const chooseLondon = () => {
  fireEvent.mouseDown(timeZoneSelect())
  fireEvent.click(screen.getByTitle('(UTC+00:00) Europe/London'))
}

// Saving a different zone asks first; this answers the question.
const confirmChange = async () => {
  const dialog = await screen.findByRole('dialog')
  await act(async () => {
    fireEvent.click(
      within(dialog).getByRole('button', { name: 'Change time zone' }),
    )
  })
}

describe('GeneralSettingsForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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
    await confirmChange()

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({ timeZone: 'Europe/London' }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('asks before changing the zone, and saves nothing when cancelled', async () => {
    // Arrange
    renderForm()

    // Act
    chooseLondon()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })
    const dialog = await screen.findByRole('dialog')
    const asked = within(dialog).queryAllByText('Change the system time zone?')
    await act(async () => {
      fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    })

    // Assert
    expect(asked).not.toHaveLength(0)
    expect(mockUpdate).not.toHaveBeenCalled()
  })

  it('saves the zone already in effect without asking', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act — choose another zone and come back, so Save is enabled on the same zone
    chooseLondon()
    fireEvent.mouseDown(timeZoneSelect())
    fireEvent.click(screen.getByTitle('(UTC-05:00) America/Chicago'))
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({ timeZone: 'America/Chicago' }),
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
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
    const dialog = await screen.findByRole('dialog')
    // antd draws a field's errors from a timer after they are set. That timer
    // must fire inside act: outside it React hands the update to its
    // scheduler, which the test setup's MessageChannel stub never runs, so the
    // error never draws however long the test waits. Fake timers fire it only
    // when advanced here; the second act runs the timer the first one's render
    // scheduled.
    vi.useFakeTimers()
    try {
      await act(async () => {
        fireEvent.click(
          within(dialog).getByRole('button', { name: 'Change time zone' }),
        )
        await vi.advanceTimersByTimeAsync(100)
      })
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100)
      })
    } finally {
      vi.useRealTimers()
    }

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      'Correct the validation error(s) to continue.',
    )
    expect(
      screen.getByText("'Mars/Base' is not a valid IANA time zone."),
    ).toBeInTheDocument()
    expect(mockSuccess).not.toHaveBeenCalled()
  })

  it("shows the server's message when the save is refused", async () => {
    // Arrange
    mockUpdate.mockResolvedValue({
      error: {
        kind: 'api-response',
        status: 403,
        detail: 'You do not have permission to do that.',
        url: '/api',
        headers: {},
      },
    })
    renderForm()

    // Act
    chooseLondon()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })
    await confirmChange()

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
