import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductDto } from '@/src/services/wayd-api'
import ChangeProductStatusForm from './change-product-status-form'

const product = {
  id: 'product-1',
  key: 7,
  name: 'Dispatch API',
  status: { id: 'status-active', name: 'Active', category: 2, alias: 1 },
  tags: [],
} as unknown as ProductDto

let isError = false

let statusOptions = [
  { id: 'status-concept', name: 'Concept', category: 1, alias: 0 },
  { id: 'status-active', name: 'Active', category: 2, alias: 1 },
  { id: 'status-retired', name: 'Retired', category: 3, alias: 3 },
]

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling
// those in initialises the store this form never touches. The form instance is
// real, though — antd's Form binds to it, and a stub object breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form: AntForm } = jest.requireActual('antd')
  return {
    useModalForm: () => {
      const [form] = AntForm.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: jest.fn(),
        handleCancel: jest.fn(),
      }
    },
  }
})

const getProductStatusOptions = jest.fn()

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useChangeProductStatusMutation: () => [jest.fn()],
  useGetProductStatusOptionsQuery: (productId: string) => {
    getProductStatusOptions(productId)
    return {
      data: isError ? undefined : statusOptions,
      isLoading: false,
      isError,
    }
  },
}))

const renderForm = () =>
  render(
    <ChangeProductStatusForm
      product={product}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

describe('ChangeProductStatusForm', () => {
  it('offers the statuses from the governing workflow', async () => {
    // Arrange
    renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByTitle('Concept')).toBeInTheDocument()
    expect(await screen.findByTitle('Retired')).toBeInTheDocument()
  })

  it('excludes the status the product already holds', async () => {
    // Moving to the current status is a no-op the aggregate ignores, so offering
    // it would promise a change that never happens.
    // Arrange
    renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(screen.queryByTitle('Active')).not.toBeInTheDocument()
  })

  it('asks for the moves allowed from this product', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(getProductStatusOptions).toHaveBeenCalledWith('product-1')
  })

  it('says so when the workflow allows no move from the current status', () => {
    // Arrange
    const options = statusOptions
    statusOptions = []

    try {
      // Act
      renderForm()

      // Assert
      expect(
        screen.getByText('The workflow allows no move from Active.'),
      ).toBeInTheDocument()
    } finally {
      statusOptions = options
    }
  })

  it('says the moves could not be loaded rather than that there are none', () => {
    // Arrange
    isError = true

    try {
      // Act
      renderForm()

      // Assert
      expect(
        screen.getByText(
          'The statuses this product can move to could not be loaded.',
        ),
      ).toBeInTheDocument()
      expect(screen.queryByText(/allows no move/)).not.toBeInTheDocument()
    } finally {
      isError = false
    }
  })

  it('names the current status so the change reads as a move', async () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('Currently Active.')).toBeInTheDocument()
  })
})
