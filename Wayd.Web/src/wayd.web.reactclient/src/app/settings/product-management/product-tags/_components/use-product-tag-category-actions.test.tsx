import { render, screen, fireEvent } from '@testing-library/react'
import { Dropdown } from 'antd'
import useProductTagCategoryActions from './use-product-tag-category-actions'
import { ProductTagCategoryActionTarget } from './types'

const mockHasPermissionClaim = jest.fn()
/** Captures which record the delete dialog was aimed at. */
const mockDeleteTarget = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs are covered by their own behaviour; here they only need to be
// identifiable so we can assert which one an action opened.
jest.mock('./edit-product-tag-category-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./delete-product-tag-category-form', () => ({
  __esModule: true,
  default: ({ category }: { category: unknown }) => {
    mockDeleteTarget(category)
    return <div>delete dialog</div>
  },
}))
jest.mock('./change-product-tag-category-active-form', () => ({
  __esModule: true,
  default: ({ isActive }: { isActive: boolean }) => (
    <div>{isActive ? 'activate' : 'deactivate'} dialog</div>
  ),
}))

const category = (
  overrides: Partial<ProductTagCategoryActionTarget> = {},
): ProductTagCategoryActionTarget => ({
  id: '8f6d4c0e-0000-0000-0000-000000000001',
  key: 3,
  name: 'Platform',
  description: 'What a product runs on',
  order: 1,
  isActive: true,
  isSystem: false,
  ...overrides,
})

/** Renders the hook's output so the menu can be opened and clicked. */
const Harness = ({
  target,
  onChanged = jest.fn(),
  onDeleted = jest.fn(),
}: {
  target: ProductTagCategoryActionTarget
  onChanged?: () => void
  onDeleted?: (id: string) => void
}) => {
  const { getActionItems, dialogs } = useProductTagCategoryActions({
    onChanged,
    onDeleted,
  })
  const actionItems = getActionItems(target)
  // The grid renders these behind its own ⋯; here a plain Dropdown stands in
  // so the items can be opened and clicked.
  return (
    <>
      {actionItems.length > 0 && (
        <Dropdown menu={{ items: actionItems }} trigger={['click']}>
          <button>Actions</button>
        </Dropdown>
      )}
      {dialogs}
    </>
  )
}

/** Opens the actions dropdown and returns the menu item labels. */
const openMenu = () => {
  fireEvent.click(screen.getByRole('button', { name: /Actions/ }))
  return Array.from(document.querySelectorAll('.ant-dropdown-menu-item')).map(
    (i) => i.textContent,
  )
}

/** Grants every permission. */
const allowAll = () => mockHasPermissionClaim.mockReturnValue(true)

/** Grants only the named permissions. */
const allowOnly = (...granted: string[]) =>
  mockHasPermissionClaim.mockImplementation((claim: string) =>
    granted.includes(claim),
  )

describe('useProductTagCategoryActions', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    allowAll()
  })

  describe('available actions', () => {
    it('offers edit, delete and deactivate on an active axis', () => {
      // Arrange / Act
      render(<Harness target={category()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Deactivate'])
    })

    it('offers activate on an inactive axis', () => {
      // Arrange / Act — deactivation is reversible, so the one action swaps
      render(<Harness target={category({ isActive: false })} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Activate'])
    })

    it('offers only deactivate on a platform-seeded axis', () => {
      // Arrange / Act — the domain refuses to edit or delete a system axis, so
      // offering either would only produce a failure. Deactivating still works.
      render(<Harness target={category({ isSystem: true })} />)

      // Assert
      expect(openMenu()).toEqual(['Deactivate'])
    })

    it('renders no menu at all for a read-only viewer', () => {
      // Arrange
      allowOnly('Permissions.ProductTagCategories.View')

      // Act
      render(<Harness target={category()} />)

      // Assert — null, not an empty menu
      expect(
        screen.queryByRole('button', { name: /Actions/ }),
      ).not.toBeInTheDocument()
    })

    it('hides delete from a viewer who may update but not delete', () => {
      // Arrange
      allowOnly('Permissions.ProductTagCategories.Update')

      // Act
      render(<Harness target={category()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Deactivate'])
    })

    it('offers only delete to a viewer who may delete but not update', () => {
      // Arrange
      allowOnly('Permissions.ProductTagCategories.Delete')

      // Act
      render(<Harness target={category()} />)

      // Assert — no Edit, and no Deactivate, which is an update
      expect(openMenu()).toEqual(['Delete'])
    })
  })

  describe('dialogs', () => {
    it('opens the edit dialog', () => {
      // Arrange
      render(<Harness target={category()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Edit'))

      // Assert
      expect(screen.getByText('edit dialog')).toBeInTheDocument()
    })

    it('opens the deactivate dialog on an active axis', () => {
      // Arrange
      render(<Harness target={category()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Deactivate'))

      // Assert
      expect(screen.getByText('deactivate dialog')).toBeInTheDocument()
    })

    it('opens the activate dialog on an inactive axis', () => {
      // Arrange
      render(<Harness target={category({ isActive: false })} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Activate'))

      // Assert
      expect(screen.getByText('activate dialog')).toBeInTheDocument()
    })

    it('aims the delete dialog at the row it was opened from', () => {
      // Arrange — the dialogs are rendered once for the whole grid, so the
      // target has to travel with the open dialog rather than the row.
      const target = category({ key: 9, name: 'Compliance' })
      render(<Harness target={target} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Delete'))

      // Assert
      expect(screen.getByText('delete dialog')).toBeInTheDocument()
      expect(mockDeleteTarget).toHaveBeenCalledWith(target)
    })
  })
})
