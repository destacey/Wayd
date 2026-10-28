import { render, screen, fireEvent } from '@testing-library/react'
import { Dropdown } from 'antd'
import useTagAxisActions from './use-tag-axis-actions'
import { TagAxisActionTarget } from './types'

const mockHasPermissionClaim = jest.fn()
/** Captures which record the delete dialog was aimed at. */
const mockDeleteTarget = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs have their own tests; here they only need to be identifiable.
jest.mock('./edit-tag-axis-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./delete-tag-axis-form', () => ({
  __esModule: true,
  default: ({
    axis,
    onFormComplete,
  }: {
    axis: unknown
    onFormComplete: () => void
  }) => {
    mockDeleteTarget(axis)
    return <button onClick={onFormComplete}>confirm delete</button>
  },
}))
jest.mock('./change-tag-axis-active-form', () => ({
  __esModule: true,
  default: ({ isActive }: { isActive: boolean }) => (
    <div>{isActive ? 'activate' : 'deactivate'} dialog</div>
  ),
}))

const axis = (
  overrides: Partial<TagAxisActionTarget> = {},
): TagAxisActionTarget => ({
  id: 'axis-1',
  name: 'Area',
  description: 'The part of the business a request touches',
  isActive: true,
  isSystem: false,
  tags: [],
  ...overrides,
})

const Harness = ({
  target,
  onDeleted = jest.fn(),
}: {
  target: TagAxisActionTarget
  onDeleted?: (id: string) => void
}) => {
  const { getActionItems, dialogs } = useTagAxisActions({ onDeleted })
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

describe('useTagAxisActions', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(true)
  })

  describe('available actions', () => {
    it('offers edit, delete and deactivate on an active axis nothing is tagged along', () => {
      // Arrange / Act
      render(<Harness target={axis()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Deactivate'])
    })

    it('offers activate on an inactive axis', () => {
      // Arrange / Act
      render(<Harness target={axis({ isActive: false })} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Activate'])
    })

    it('does not offer delete on an axis whose tags requests carry', () => {
      // Arrange — the API refuses it, so deactivating is the way out.
      const inUse = axis({
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

    it('offers only deactivate on a platform-defined axis', () => {
      // Arrange / Act — the domain refuses to edit or delete a system axis.
      render(<Harness target={axis({ isSystem: true })} />)

      // Assert
      expect(openMenu()).toEqual(['Deactivate'])
    })

    it('renders no menu for a viewer who cannot administer requests', () => {
      // Arrange
      mockHasPermissionClaim.mockReturnValue(false)

      // Act
      render(<Harness target={axis()} />)

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
      render(<Harness target={axis()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Edit'))

      // Assert
      expect(screen.getByText('edit dialog')).toBeInTheDocument()
    })

    it('opens the deactivate dialog on an active axis', () => {
      // Arrange
      render(<Harness target={axis()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Deactivate'))

      // Assert
      expect(screen.getByText('deactivate dialog')).toBeInTheDocument()
    })

    it('opens the activate dialog on an inactive axis', () => {
      // Arrange
      render(<Harness target={axis({ isActive: false })} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Activate'))

      // Assert
      expect(screen.getByText('activate dialog')).toBeInTheDocument()
    })

    it('aims the delete dialog at the row it came from and reports the deleted id', () => {
      // Arrange
      const onDeleted = jest.fn()
      const target = axis({ id: 'axis-9', name: 'Customer segment' })
      render(<Harness target={target} onDeleted={onDeleted} />)
      openMenu()
      fireEvent.click(screen.getByText('Delete'))

      // Act
      fireEvent.click(screen.getByText('confirm delete'))

      // Assert
      expect(mockDeleteTarget).toHaveBeenCalledWith(target)
      expect(onDeleted).toHaveBeenCalledWith('axis-9')
    })
  })
})
