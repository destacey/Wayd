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

const statusOptions = [
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

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useChangeProductStatusMutation: () => [jest.fn()],
  useGetProductStatusOptionsQuery: () => ({
    data: statusOptions,
    isLoading: false,
  }),
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

  it('names the current status so the change reads as a move', async () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('Currently Active.')).toBeInTheDocument()
  })
})
