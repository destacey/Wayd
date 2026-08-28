import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { Dropdown } from 'antd'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import useEstimationScaleActions from './use-estimation-scale-actions'

const mockHasPermissionClaim = jest.fn()
const mockSetActiveStatus = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()
/** Captures which record the delete dialog was aimed at. */
const mockDeleteTarget = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

jest.mock('@/src/store/features/planning/estimation-scales-api', () => ({
  useSetEstimationScaleActiveStatusMutation: () => [mockSetActiveStatus],
}))

// The dialogs are covered by their own tests; here they only need to be
// identifiable so we can assert which one an action opened.
jest.mock('./edit-estimation-scale-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./delete-estimation-scale-form', () => ({
  __esModule: true,
  default: ({ estimationScale }: { estimationScale: unknown }) => {
    mockDeleteTarget(estimationScale)
    return <div>delete dialog</div>
  },
}))

const scale = (overrides: Partial<EstimationScaleDto> = {}): EstimationScaleDto => ({
  id: 3,
  name: 'Fibonacci',
  description: 'Classic planning poker deck',
  isActive: true,
  values: ['1', '2', '3', '5', '8'],
  ...overrides,
})

const Harness = ({
  estimationScale,
  onChanged = jest.fn(),
  onDeleted = jest.fn(),
}: {
  estimationScale: EstimationScaleDto
  onChanged?: () => void
  onDeleted?: (id: number) => void
}) => {
  const { getActionItems, dialogs } = useEstimationScaleActions({
    onChanged,
    onDeleted,
  })
  const items = getActionItems(estimationScale)
  return (
    <>
      {items.length > 0 && (
        <Dropdown menu={{ items }} trigger={['click']}>
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

const allowAll = () => mockHasPermissionClaim.mockReturnValue(true)
const allowOnly = (...granted: string[]) =>
  mockHasPermissionClaim.mockImplementation((claim: string) =>
    granted.includes(claim),
  )

describe('useEstimationScaleActions', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    allowAll()
    mockSetActiveStatus.mockResolvedValue({ data: {} })
  })

  describe('available actions', () => {
    it('offers edit, deactivate and delete on an active scale', () => {
      // Arrange / Act
      render(<Harness estimationScale={scale()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Deactivate', 'Delete'])
    })

    it('offers Activate on an inactive scale', () => {
      // Arrange / Act — the same item, labelled by what it will do
      render(<Harness estimationScale={scale({ isActive: false })} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Activate', 'Delete'])
    })

    it('renders no menu at all for a read-only viewer', () => {
      // Arrange
      allowOnly('Permissions.EstimationScales.View')

      // Act
      render(<Harness estimationScale={scale()} />)

      // Assert
      expect(
        screen.queryByRole('button', { name: /Actions/ }),
      ).not.toBeInTheDocument()
    })

    it('hides delete from a viewer who may update but not delete', () => {
      // Arrange
      allowOnly('Permissions.EstimationScales.Update')

      // Act
      render(<Harness estimationScale={scale()} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Deactivate'])
    })

    it('offers only delete to a viewer who may delete but not update', () => {
      // Arrange
      allowOnly('Permissions.EstimationScales.Delete')

      // Act
      render(<Harness estimationScale={scale()} />)

      // Assert
      expect(openMenu()).toEqual(['Delete'])
    })
  })

  describe('activation', () => {
    it('toggles active without a confirmation dialog', async () => {
      // Arrange — reversible from the same menu item, so a confirm would only
      // add a click.
      const onChanged = jest.fn()
      render(<Harness estimationScale={scale()} onChanged={onChanged} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Deactivate'))

      // Assert
      await waitFor(() =>
        expect(mockSetActiveStatus).toHaveBeenCalledWith({
          id: 3,
          isActive: false,
        }),
      )
      expect(onChanged).toHaveBeenCalled()
      expect(mockSuccess).toHaveBeenCalled()
    })

    it('reports a failed toggle and does not claim success', async () => {
      // Arrange
      mockSetActiveStatus.mockResolvedValue({ error: new Error('nope') })
      const onChanged = jest.fn()
      render(<Harness estimationScale={scale()} onChanged={onChanged} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Deactivate'))

      // Assert
      await waitFor(() => expect(mockError).toHaveBeenCalled())
      expect(mockSuccess).not.toHaveBeenCalled()
      expect(onChanged).not.toHaveBeenCalled()
    })
  })

  describe('dialogs', () => {
    it('opens the edit dialog', () => {
      // Arrange
      render(<Harness estimationScale={scale()} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Edit'))

      // Assert
      expect(screen.getByText('edit dialog')).toBeInTheDocument()
    })

    it('opens the delete dialog against the record whose menu was used', () => {
      // Arrange — the grid builds a ⋯ per row from the same hook
      render(<Harness estimationScale={scale({ id: 9, name: 'T-Shirt' })} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Delete'))

      // Assert
      expect(screen.getByText('delete dialog')).toBeInTheDocument()
      expect(mockDeleteTarget).toHaveBeenLastCalledWith(
        expect.objectContaining({ id: 9, name: 'T-Shirt' }),
      )
    })

    it('holds one dialog at a time', () => {
      // Arrange — the state is one value, not a boolean per dialog
      render(<Harness estimationScale={scale()} />)
      openMenu()
      fireEvent.click(screen.getByText('Edit'))

      // Act
      openMenu()
      fireEvent.click(screen.getByText('Delete'))

      // Assert
      expect(screen.getByText('delete dialog')).toBeInTheDocument()
      expect(screen.queryByText('edit dialog')).not.toBeInTheDocument()
    })
  })
})
