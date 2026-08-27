import { render, screen, fireEvent } from '@testing-library/react'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import useExpenditureCategoryActions from './use-expenditure-category-actions'

const mockHasPermissionClaim = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs are covered by their own tests; here they only need to be
// identifiable so we can assert which one an action opened.
jest.mock('./edit-expenditure-category-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./delete-expenditure-category-form', () => ({
  __esModule: true,
  default: () => <div>delete dialog</div>,
}))
jest.mock('./change-expenditure-category-state-form', () => ({
  __esModule: true,
  default: ({ stateAction }: { stateAction: string }) => (
    <div>{stateAction} dialog</div>
  ),
  ExpenditureCategoryStateAction: { Activate: 'Activate', Archive: 'Archive' },
}))

const category = (
  state: string,
  overrides: Partial<ExpenditureCategoryDetailsDto> = {},
): ExpenditureCategoryDetailsDto => ({
  id: 4,
  name: 'Capital',
  description: 'Capitalized delivery spend',
  state: { id: 1, name: state },
  isCapitalizable: true,
  requiresDepreciation: false,
  accountingCode: '4100',
  ...overrides,
})

/** Renders the hook's output so the menu can be opened and clicked. */
const Harness = ({
  expenditureCategory,
  onChanged = jest.fn(),
  onDeleted = jest.fn(),
}: {
  expenditureCategory: ExpenditureCategoryDetailsDto | undefined
  onChanged?: () => void
  onDeleted?: () => void
}) => {
  const { actions, dialogs } = useExpenditureCategoryActions({
    expenditureCategory,
    onChanged,
    onDeleted,
  })
  return (
    <>
      {actions}
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

describe('useExpenditureCategoryActions', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    allowAll()
  })

  describe('available actions', () => {
    it('offers delete and activate on a proposed category', () => {
      // Arrange / Act
      render(<Harness expenditureCategory={category('Proposed')} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Delete', 'Activate'])
    })

    it('offers archive on an active category', () => {
      // Arrange / Act — an active category cannot be deleted or re-activated
      render(<Harness expenditureCategory={category('Active')} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Archive'])
    })

    it('offers only edit on an archived category', () => {
      // Arrange / Act — archived is terminal
      render(<Harness expenditureCategory={category('Archived')} />)

      // Assert
      expect(openMenu()).toEqual(['Edit'])
    })

    it('renders no menu at all for a read-only viewer', () => {
      // Arrange
      allowOnly('Permissions.ExpenditureCategories.View')

      // Act
      render(<Harness expenditureCategory={category('Proposed')} />)

      // Assert — null, not an empty menu: the panel draws a bordered actions
      // strip whenever actions is truthy.
      expect(
        screen.queryByRole('button', { name: /Actions/ }),
      ).not.toBeInTheDocument()
    })

    it('hides delete from a viewer who may update but not delete', () => {
      // Arrange
      allowOnly('Permissions.ExpenditureCategories.Update')

      // Act
      render(<Harness expenditureCategory={category('Proposed')} />)

      // Assert
      expect(openMenu()).toEqual(['Edit', 'Activate'])
    })

    it('offers only delete to a viewer who may delete but not update', () => {
      // Arrange
      allowOnly('Permissions.ExpenditureCategories.Delete')

      // Act
      render(<Harness expenditureCategory={category('Proposed')} />)

      // Assert — no Edit, and no Activate, which is an update
      expect(openMenu()).toEqual(['Delete'])
    })

    it('renders nothing while no record is selected', () => {
      // Arrange / Act
      render(<Harness expenditureCategory={undefined} />)

      // Assert
      expect(
        screen.queryByRole('button', { name: /Actions/ }),
      ).not.toBeInTheDocument()
    })
  })

  describe('dialogs', () => {
    it('opens the edit dialog', () => {
      // Arrange
      render(<Harness expenditureCategory={category('Active')} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Edit'))

      // Assert
      expect(screen.getByText('edit dialog')).toBeInTheDocument()
    })

    it('opens the activate dialog on a proposed category', () => {
      // Arrange
      render(<Harness expenditureCategory={category('Proposed')} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Activate'))

      // Assert
      expect(screen.getByText('Activate dialog')).toBeInTheDocument()
    })

    it('opens the archive dialog on an active category', () => {
      // Arrange
      render(<Harness expenditureCategory={category('Active')} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Archive'))

      // Assert
      expect(screen.getByText('Archive dialog')).toBeInTheDocument()
    })

    it('opens the delete dialog', () => {
      // Arrange
      render(<Harness expenditureCategory={category('Proposed')} />)
      openMenu()

      // Act
      fireEvent.click(screen.getByText('Delete'))

      // Assert
      expect(screen.getByText('delete dialog')).toBeInTheDocument()
    })

    it('holds one dialog at a time', () => {
      // Arrange — the state is one value, not a boolean per dialog, so two
      // cannot be open at once.
      render(<Harness expenditureCategory={category('Proposed')} />)
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
