import { fireEvent, render, screen } from '@testing-library/react'
import ManageTeamProductsButton from './manage-team-products-button'

let mockCanUpdate = true
const manageForm = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => mockCanUpdate }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useGetTeamProductsQuery: () => ({
    data: [
      { product: { id: 'checkout' }, team: { id: 'payments' } },
      { product: { id: 'web' }, team: { id: 'payments' } },
    ],
    isLoading: false,
  }),
}))

// The dialog is tested on its own; here only what the button hands it matters.
jest.mock('./manage-team-products-form', () => ({
  __esModule: true,
  default: (props: unknown) => {
    manageForm(props)
    return <div>Manage dialog</div>
  },
}))

beforeEach(() => {
  jest.clearAllMocks()
  mockCanUpdate = true
})

describe('ManageTeamProductsButton', () => {
  it("opens Manage Products with the team's current products", () => {
    // Arrange
    render(<ManageTeamProductsButton teamId="payments" teamName="Payments" />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Manage Products' }))

    // Assert
    expect(screen.getByText('Manage dialog')).toBeInTheDocument()
    expect(manageForm).toHaveBeenCalledWith(
      expect.objectContaining({
        teamId: 'payments',
        teamName: 'Payments',
        currentProductIds: ['checkout', 'web'],
      }),
    )
  })

  it('renders nothing without permission to update products', () => {
    // Arrange
    mockCanUpdate = false

    // Act
    const { container } = render(
      <ManageTeamProductsButton teamId="payments" teamName="Payments" />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
