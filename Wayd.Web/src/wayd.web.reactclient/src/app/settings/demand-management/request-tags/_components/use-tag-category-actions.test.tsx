import { render, screen, fireEvent } from '@testing-library/react'
import { Dropdown } from 'antd'
import useTagCategoryActions from './use-tag-category-actions'
import { TagCategoryActionTarget } from './types'

const mockHasPermissionClaim = vi.fn()
/** Captures which record the delete dialog was aimed at. */
const mockDeleteTarget = vi.fn()

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs have their own tests; here they only need to be identifiable.
vi.mock('./edit-tag-category-form', () => ({
  default: () => <div>edit dialog</div>,
}))
vi.mock('./delete-tag-category-form', () => ({
  default: ({
    category,
    onFormComplete,
  }: {
    category: unknown
    onFormComplete: () => void
  }) => {
    mockDeleteTarget(category)
    return <button onClick={onFormComplete}>confirm delete</button>
  },
}))
vi.mock('./change-tag-category-active-form', () => ({
  default: ({ isActive }: { isActive: boolean }) => (
    <div>{isActive ? 'activate' : 'deactivate'} dialog</div>
  ),
}))

const category = (
  overrides: Partial<TagCategoryActionTarget> = {},
): TagCategoryActionTarget => ({
  id: 'category-1',
  name: 'Area',
  description: 'The part of the business a request touches',
  isActive: true,
  isSystem: false,
  tags: [],
  ...overrides,
})

const Harness = ({
  target,
  onDeleted = vi.fn(),
}: {
  target: TagCategoryActionTarget
  onDeleted?: (id: string) => void
}) => {
  const { getActionItems, dialogs } = useTagCategoryActions({ onDeleted })
  const actionItems = getActionItems(target)
  // The grid renders these behind its own ⋯; a plain Dropdown stands in.
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

describe('useTagCategoryActions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(true)
  })

  describe('available actions', () => {
    it('offers edit, delete and deactivate on an active category nothing is tagged in', () => {
      // Arrange / Act
      render(<Harness target={category()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Deactivate'])
    })

    it('offers activate on an inactive category', () => {
      // Arrange / Act
      render(<Harness target={category({ isActive: false })} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Activate'])
    })

    it('does not offer delete on a category whose tags requests carry', () => {
      // Arrange — the API refuses it, so deactivating is the way out.
      const inUse = category({
        tags: [
          { id: 't-1', name: 'Payments', isActive: true, requestCount: 0 },
          { id: 't-2', name: 'Lending', isActive: true, requestCount: 2 },
        ],
      })

      // Act
      render(<Harness target={inUse} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Deactivate'])
    })

    it('offers only deactivate on a platform-defined category', () => {
      // Arrange / Act — the domain refuses to edit or delete a system category.
      render(<Harness target={category({ isSystem: true })} />)

      // Assert
      expect(openMenu()).toEqual(['Deactivate'])
    })

    it('renders no menu for a viewer who cannot administer requests', () => {
      // Arrange
      mockHasPermissionClaim.mockReturnValue(false)

      // Act
      render(<Harness target={category()} />)

      // Assert
      expect(
        screen.queryByRole('button', { name: /Actions/ }),
      ).not.toBeInTheDocument()
      expect(mockHasPermissionClaim).toHaveBeenCalledWith(
        'Permissions.IntakeRequests.Administer',
      )
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

    it('opens the deactivate dialog on an active category', () => {
      // Arrange
      render(<Harness target={category()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Deactivate'))

      // Assert
      expect(screen.getByText('deactivate dialog')).toBeInTheDocument()
    })

    it('opens the activate dialog on an inactive category', () => {
      // Arrange
      render(<Harness target={category({ isActive: false })} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Activate'))

      // Assert
      expect(screen.getByText('activate dialog')).toBeInTheDocument()
    })

    it('aims the delete dialog at the row it came from and reports the deleted id', () => {
      // Arrange
      const onDeleted = vi.fn()
      const target = category({ id: 'category-9', name: 'Customer segment' })
      render(<Harness target={target} onDeleted={onDeleted} />)
      openMenu()
      fireEvent.click(screen.getByText('Delete'))

      // Act
      fireEvent.click(screen.getByText('confirm delete'))

      // Assert
      expect(mockDeleteTarget).toHaveBeenCalledWith(target)
      expect(onDeleted).toHaveBeenCalledWith('category-9')
    })
  })
})
