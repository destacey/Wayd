import { act, fireEvent, render, screen } from '@testing-library/react'
import { ProductDto } from '@/src/services/wayd-api'
import ManageTeamProductsForm, { toTreeData } from './manage-team-products-form'
import { buildProductTree } from '../../_components'

const setTeamProducts = jest.fn()
const messageError = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: messageError, success: jest.fn() }),
}))

const product = (id: string, name: string, parentId?: string) =>
  ({
    id,
    key: id.length,
    name,
    parent: parentId ? { id: parentId, key: 1, name: parentId } : undefined,
    teams: [],
    tags: [],
  }) as unknown as ProductDto

// Storefront contains Web and Checkout; Identity Service stands alone.
const PRODUCTS = [
  product('storefront', 'Storefront'),
  product('web', 'Storefront Web', 'storefront'),
  product('checkout', 'Checkout', 'storefront'),
  product('identity', 'Identity Service'),
]

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useGetProductsQuery: () => ({ data: PRODUCTS, isLoading: false }),
  useSetTeamProductsMutation: () => [setTeamProducts],
}))

/** The save handler from the live dialog, so a test need not click through the modal. */
let save: (() => Promise<void>) | undefined

jest.mock('@/src/hooks', () => ({
  useConfirmModal: ({ onSubmit }: { onSubmit: () => Promise<boolean> }) => {
    const handleOk = async () => {
      await onSubmit()
    }
    save = handleOk
    return { isOpen: true, isSaving: false, handleOk, handleCancel: jest.fn() }
  },
}))

const renderForm = (currentProductIds: string[]) =>
  render(
    <ManageTeamProductsForm
      teamId="payments"
      teamName="Payments"
      currentProductIds={currentProductIds}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

/** The checkbox on a product's tree node. */
const checkboxOf = (name: string) =>
  screen
    .getByTitle(name)
    .closest('.ant-tree-treenode')!
    .querySelector('.ant-tree-checkbox') as HTMLElement

const isChecked = (name: string) =>
  checkboxOf(name).classList.contains('ant-tree-checkbox-checked')

beforeEach(() => {
  jest.clearAllMocks()
  setTeamProducts.mockResolvedValue({ data: undefined })
})

describe('ManageTeamProductsForm', () => {
  it("starts with the team's current products checked, and nothing to save", () => {
    // Arrange / Act
    renderForm(['checkout'])

    // Assert
    expect(isChecked('Checkout')).toBe(true)
    expect(isChecked('Storefront')).toBe(false)
    expect(screen.getByText('No changes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('checks a parent without checking its children, since a link is on one product', () => {
    // Arrange
    renderForm([])

    // Act
    fireEvent.click(checkboxOf('Storefront'))

    // Assert
    expect(isChecked('Storefront')).toBe(true)
    expect(isChecked('Storefront Web')).toBe(false)
    expect(screen.getByText('1 to link, 0 to unlink')).toBeInTheDocument()
  })

  it('saves the whole set: the products kept and added, without the ones unchecked', async () => {
    // Arrange
    renderForm(['checkout', 'identity'])
    fireEvent.click(checkboxOf('Identity Service'))
    fireEvent.click(checkboxOf('Storefront Web'))

    // Act
    await act(async () => {
      await save!()
    })

    // Assert
    expect(screen.getByText('1 to link, 1 to unlink')).toBeInTheDocument()
    expect(setTeamProducts).toHaveBeenCalledWith({
      teamId: 'payments',
      productIds: ['checkout', 'web'],
    })
  })

  it('keeps a checked product the search has hidden', async () => {
    // Arrange
    renderForm(['identity'])

    // Act — search down to Checkout and check it while Identity Service is out of view
    fireEvent.change(screen.getByLabelText('Search products'), {
      target: { value: 'check' },
    })
    fireEvent.click(checkboxOf('Checkout'))
    await act(async () => {
      await save!()
    })

    // Assert
    expect(screen.queryByTitle('Identity Service')).not.toBeInTheDocument()
    expect(setTeamProducts).toHaveBeenCalledWith({
      teamId: 'payments',
      productIds: ['identity', 'checkout'],
    })
  })

  it('shows the refusal the API gives', async () => {
    // Arrange
    setTeamProducts.mockResolvedValue({
      error: {
        status: 400,
        detail: 'An inactive team cannot be linked to a product.',
      },
    })
    renderForm([])
    fireEvent.click(checkboxOf('Checkout'))

    // Act
    await act(async () => {
      await save!()
    })

    // Assert
    expect(messageError).toHaveBeenCalledWith(
      'An inactive team cannot be linked to a product.',
    )
  })
})

describe('toTreeData', () => {
  it('keeps the ancestors of a match, so it is shown where it sits', () => {
    // Act
    const data = toTreeData(buildProductTree(PRODUCTS), 'web')

    // Assert
    expect(data).toEqual([
      {
        key: 'storefront',
        title: 'Storefront',
        children: [{ key: 'web', title: 'Storefront Web', children: [] }],
      },
    ])
  })

  it('orders each level by name', () => {
    // Act
    const data = toTreeData(buildProductTree(PRODUCTS), '')

    // Assert
    expect(data.map((n) => n.title)).toEqual(['Identity Service', 'Storefront'])
    expect(data[1].children!.map((n) => n.title)).toEqual([
      'Checkout',
      'Storefront Web',
    ])
  })
})
