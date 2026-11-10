import { render, screen, fireEvent } from '@testing-library/react'
import { Dropdown } from 'antd'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import useExpenditureCategoryActions from './use-expenditure-category-actions'

const mockHasPermissionClaim = vi.fn()
/** Captures which record the delete dialog was aimed at. */
const mockDeleteTarget = vi.fn()

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs are covered by their own tests; here they only need to be
// identifiable so we can assert which one an action opened.
vi.mock('./edit-expenditure-category-form', () => ({
  default: () => <div>edit dialog</div>,
}))
vi.mock('./delete-expenditure-category-form', () => ({
  default: ({ expenditureCategory }: { expenditureCategory: unknown }) => {
    mockDeleteTarget(expenditureCategory)
    return <div>delete dialog</div>
  },
}))
vi.mock('./change-expenditure-category-state-form', () => ({
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
  onChanged = vi.fn(),
  onDeleted = vi.fn(),
}: {
  expenditureCategory: ExpenditureCategoryDetailsDto | undefined
  onChanged?: () => void
  onDeleted?: (id: number) => void
}) => {
  const { getActionItems, dialogs } = useExpenditureCategoryActions({
    onChanged,
    onDeleted,
  })
  const actionItems = expenditureCategory
    ? getActionItems(expenditureCategory)
    : []
  // The panel renders these behind its own ⋯; here a plain Dropdown stands in
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

describe('useExpenditureCategoryActions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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

    it('targets the record whose menu was used, not a fixed one', () => {
      // Arrange — the grid builds a ⋯ per row from the same hook, so the
      // dialog has to open against whichever row was clicked.
      const onDeleted = vi.fn()
      const rowA = category('Proposed', { id: 1, name: 'Capital' })
      const rowB = category('Proposed', { id: 2, name: 'Operating' })
      const Rows = () => {
        const { getActionItems, dialogs } = useExpenditureCategoryActions({
          onChanged: vi.fn(),
          onDeleted,
        })
        return (
          <>
            {[rowA, rowB].map((row) => (
              <Dropdown
                key={row.id}
                menu={{ items: getActionItems(row) }}
                trigger={['click']}
              >
                <button>{`Actions ${row.name}`}</button>
              </Dropdown>
            ))}
            {dialogs}
          </>
        )
      }
      render(<Rows />)

      // Act — the second row's menu
      fireEvent.click(screen.getByRole('button', { name: 'Actions Operating' }))
      fireEvent.click(screen.getByText('Delete'))

      // Assert — one set of dialogs is mounted, aimed at row B
      expect(screen.getByText('delete dialog')).toBeInTheDocument()
      expect(mockDeleteTarget).toHaveBeenLastCalledWith(
        expect.objectContaining({ id: 2, name: 'Operating' }),
      )
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
