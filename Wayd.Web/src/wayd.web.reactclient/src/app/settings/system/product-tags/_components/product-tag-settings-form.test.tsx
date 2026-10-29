import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ProductTagSettingsDto } from '@/src/services/wayd-api'
import ProductTagSettingsForm, {
  ProductTagSettingsFormProps,
} from './product-tag-settings-form'

const mockUpdate = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useUpdateProductTagSettingsMutation: () => [mockUpdate, { isLoading: false }],
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const settings: ProductTagSettingsDto = { allowsFreeTags: false }

const renderForm = (overrides: Partial<ProductTagSettingsFormProps> = {}) =>
  render(
    <ProductTagSettingsForm
      settings={settings}
      isLoading={false}
      canUpdate={true}
      {...overrides}
    />,
  )

const freeTagsSwitch = () =>
  screen.getByRole('switch', { name: 'Allow free tags on products' })

describe('ProductTagSettingsForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows the saved value', () => {
    // Arrange / Act
    renderForm({ settings: { allowsFreeTags: true } })

    // Assert
    expect(freeTagsSwitch()).toBeChecked()
  })

  it('keeps Save disabled until the value changes', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('saves free tags turned on', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ data: undefined })
    renderForm()

    // Act
    fireEvent.click(freeTagsSwitch())
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({ allowsFreeTags: true }),
    )
    expect(mockSuccess).toHaveBeenCalled()
  })

  it('reports a failed save with its detail', async () => {
    // Arrange
    mockUpdate.mockResolvedValue({ error: { status: 400, detail: 'Nope.' } })
    renderForm()

    // Act
    fireEvent.click(freeTagsSwitch())
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    await waitFor(() => expect(mockError).toHaveBeenCalledWith('Nope.'))
    expect(mockSuccess).not.toHaveBeenCalled()
  })

  it('is read-only without the update permission', () => {
    // Arrange / Act
    renderForm({ canUpdate: false })

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Save' }),
    ).not.toBeInTheDocument()
    expect(freeTagsSwitch()).toBeDisabled()
  })
})
