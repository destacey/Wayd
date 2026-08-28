import { render, screen, fireEvent } from '@testing-library/react'
import { RoleDto } from '@/src/services/wayd-api'
import Permissions from './permissions'

const mockHasPermissionClaim = jest.fn()
const mockPermissionsQuery = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

jest.mock('@/src/store/features/user-management/permissions-api', () => ({
  useGetPermissionsQuery: () => mockPermissionsQuery(),
}))

jest.mock('@/src/store/features/user-management/roles-api', () => ({
  useUpdatePermissionsMutation: () => [jest.fn()],
}))

const role = { id: 'r1', name: 'Delivery Lead' } as RoleDto

const PERMISSIONS = [
  {
    name: 'Permissions.Projects.View',
    description: 'View projects',
    category: 'PPM',
    resource: 'Projects',
  },
  {
    name: 'Permissions.Projects.Update',
    description: 'Update projects',
    category: 'PPM',
    resource: 'Projects',
  },
]

/** Stands in for the record section rail, which is what the guard must catch. */
const Rail = () => (
  <div role="tablist">
    <div role="tab" aria-selected="true">
      Permissions
    </div>
    <div role="tab" aria-selected="false">
      Users
    </div>
  </div>
)

const renderWithRail = () =>
  render(
    <>
      <Rail />
      <Permissions role={role} permissions={[]} isSystemRole={false} />
    </>,
  )

/**
 * Puts the editor into a dirty state.
 *
 * The switches live inside collapse panels, so this uses Select All instead —
 * it sets every permission in one go, which is a change against the role's
 * empty starting set.
 */
const makeDirty = () => {
  fireEvent.click(screen.getByRole('button', { name: 'Manage Permissions' }))
  fireEvent.click(screen.getByText('Select All'))
}

describe('Permissions', () => {
  const confirmSpy = jest.spyOn(window, 'confirm')

  beforeEach(() => {
    jest.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(true)
    mockPermissionsQuery.mockReturnValue({
      data: PERMISSIONS,
      isLoading: false,
    })
    confirmSpy.mockReturnValue(true)
  })

  afterAll(() => {
    confirmSpy.mockRestore()
  })

  describe('unsaved-changes guard', () => {
    it('does not prompt while nothing has changed', () => {
      // Arrange
      renderWithRail()

      // Act — move to the other section without editing anything
      fireEvent.click(screen.getByText('Users'))

      // Assert
      expect(confirmSpy).not.toHaveBeenCalled()
    })

    it('prompts when a section change would discard edits', () => {
      // Arrange — the rail's entries are role="tab" buttons, not anchors, so
      // the anchor guard alone never saw them.
      renderWithRail()
      makeDirty()

      // Act
      fireEvent.click(screen.getByText('Users'))

      // Assert
      expect(confirmSpy).toHaveBeenCalledWith(
        expect.stringContaining('unsaved permission changes'),
      )
    })

    it('does not prompt when re-selecting the section already open', () => {
      // Arrange — clicking the current section changes nothing, so a prompt
      // would be pure friction.
      renderWithRail()
      makeDirty()

      // Act
      fireEvent.click(screen.getByText('Permissions'))

      // Assert
      expect(confirmSpy).not.toHaveBeenCalled()
    })

    it('prompts on a keyboard section change too', () => {
      // Arrange — the rail handles Enter and Space, which produce no click
      renderWithRail()
      makeDirty()

      // Act
      fireEvent.keyDown(screen.getByText('Users'), { key: 'Enter' })

      // Assert
      expect(confirmSpy).toHaveBeenCalled()
    })

    it('ignores keys that do not activate a rail entry', () => {
      // Arrange
      renderWithRail()
      makeDirty()

      // Act
      fireEvent.keyDown(screen.getByText('Users'), { key: 'ArrowDown' })

      // Assert
      expect(confirmSpy).not.toHaveBeenCalled()
    })
  })
})
