import { act, render } from '@testing-library/react'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import UnlinkProductTeamForm from './unlink-product-team-form'

const mutate = jest.fn()
const messageError = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: messageError, success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useUnlinkProductTeamMutation: () => [mutate],
}))

/** The confirm handler from the live dialog, so a test need not click through the modal. */
let confirm: (() => Promise<void>) | undefined

jest.mock('@/src/hooks', () => ({
  useConfirmModal: ({ onSubmit }: { onSubmit: () => Promise<boolean> }) => {
    const handleOk = async () => {
      await onSubmit()
    }
    confirm = handleOk
    return {
      isOpen: true,
      isSaving: false,
      handleOk,
      handleCancel: jest.fn(),
    }
  },
}))

// A rolled-up row: the link is on Checkout, beneath the product page it was read from.
const link = {
  product: { id: 'checkout', key: 5, name: 'Checkout' },
  productPath: [{ id: 'web', key: 2, name: 'Storefront Web' }],
  team: { id: 'payments', key: 7, name: 'Payments', code: 'PAY' },
} as unknown as ProductTeamLinkDto

const renderForm = () =>
  render(
    <UnlinkProductTeamForm
      link={link}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

beforeEach(() => {
  jest.clearAllMocks()
  mutate.mockResolvedValue({ data: undefined })
})

describe('UnlinkProductTeamForm', () => {
  it('unlinks the team from the product the link is on, not the page it was read from', async () => {
    // Arrange
    renderForm()

    // Act
    await act(async () => {
      await confirm!()
    })

    // Assert
    expect(mutate).toHaveBeenCalledWith({
      productId: 'checkout',
      teamId: 'payments',
    })
  })

  it('shows the refusal the API gives', async () => {
    // Arrange
    mutate.mockResolvedValue({
      error: { status: 400, detail: 'Product not found.' },
    })
    renderForm()

    // Act
    await act(async () => {
      await confirm!()
    })

    // Assert
    expect(messageError).toHaveBeenCalledWith('Product not found.')
  })
})
