import { fireEvent, render, screen } from '@testing-library/react'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import FreeProductTagsSection, {
  freeTagActionItems,
} from './free-product-tags-section'

const mockHasPermissionClaim = jest.fn()
let mockAllowsFreeTags: boolean | undefined = true
let mockSearchFailed = false
let mockSettingsFailed = false

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

jest.mock('@/src/store/features/product-management/product-tags-api', () => ({
  useSearchProductTagsQuery: () => ({
    data: mockSearchFailed ? undefined : [tag],
    isLoading: false,
    isError: mockSearchFailed,
    refetch: jest.fn(),
  }),
  useGetProductTagsSettingsQuery: () => ({
    data:
      mockAllowsFreeTags === undefined
        ? undefined
        : { allowsFreeTags: mockAllowsFreeTags },
    isLoading: false,
    isError: mockSettingsFailed,
  }),
}))

// The grid and dialogs are covered on their own; here only what the section
// decides — the note and the actions — is under test.
jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: () => <div>grid</div>,
}))
jest.mock('./edit-free-product-tag-form', () => ({
  __esModule: true,
  default: () => null,
}))
jest.mock('./merge-product-tag-form', () => ({
  __esModule: true,
  default: () => null,
}))
jest.mock('./delete-free-product-tag-form', () => ({
  __esModule: true,
  default: () => null,
}))
jest.mock('./product-tag-settings-form', () => ({
  __esModule: true,
  default: ({ canUpdate }: { canUpdate: boolean }) => (
    <div>{canUpdate ? 'editable setting' : 'read-only setting'}</div>
  ),
}))
jest.mock('./product-tag-settings-history-drawer', () => ({
  __esModule: true,
  default: ({ open }: { open: boolean }) => (open ? <div>history</div> : null),
}))

const tag: ProductTagListItemDto = {
  id: 'free-wave',
  name: 'migration wave',
  isFree: true,
  isActive: true,
  productCount: 3,
}

const keys = (permissions: { canUpdate: boolean; canDelete: boolean }) =>
  freeTagActionItems(tag, permissions, jest.fn()).map((item) => item?.key)

describe('freeTagActionItems', () => {
  it('offers edit and merge with Update, and delete with Delete', () => {
    // Act / Assert
    expect(keys({ canUpdate: true, canDelete: true })).toEqual([
      'edit',
      'merge',
      'delete',
    ])
    expect(keys({ canUpdate: true, canDelete: false })).toEqual([
      'edit',
      'merge',
    ])
    expect(keys({ canUpdate: false, canDelete: true })).toEqual(['delete'])
  })

  it('offers nothing to a viewer who can change nothing', () => {
    // Act / Assert
    expect(keys({ canUpdate: false, canDelete: false })).toEqual([])
  })

  it('opens the dialog for the tag it was built for', () => {
    // Arrange
    const open = jest.fn()
    const [, merge] = freeTagActionItems(
      tag,
      { canUpdate: true, canDelete: false },
      open,
    ) as unknown as { onClick: () => void }[]

    // Act
    merge.onClick()

    // Assert
    expect(open).toHaveBeenCalledWith('merge', tag)
  })
})

describe('FreeProductTagsSection', () => {
  beforeEach(() => {
    mockHasPermissionClaim.mockReset().mockReturnValue(true)
    mockAllowsFreeTags = true
    mockSearchFailed = false
    mockSettingsFailed = false
  })

  it('offers the setting, editable, to a viewer who can update system settings', () => {
    // Arrange / Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText('editable setting')).toBeInTheDocument()
    expect(screen.queryByText(/Free tags are on/)).not.toBeInTheDocument()
  })

  it('shows the setting read-only to a viewer who can only view system settings', () => {
    // Arrange
    mockHasPermissionClaim.mockImplementation(
      (claim: string) => claim !== 'Permissions.SystemSettings.Update',
    )

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText('read-only setting')).toBeInTheDocument()
  })

  it('opens the setting history', () => {
    // Arrange
    render(<FreeProductTagsSection />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Setting history' }))

    // Assert
    expect(screen.getByText('history')).toBeInTheDocument()
  })

  it('tells a viewer who cannot see settings whether free tags are on, without the switch or history', () => {
    // The setting is read from the product tags endpoint, open to anyone on this page.
    // Arrange
    mockHasPermissionClaim.mockImplementation(
      (claim: string) => !claim.startsWith('Permissions.SystemSettings.'),
    )

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText(/Free tags are on/)).toBeInTheDocument()
    expect(screen.queryByText(/setting$/)).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Setting history' }),
    ).not.toBeInTheDocument()
  })

  it('says free tags are off when they are', () => {
    // Arrange
    mockAllowsFreeTags = false
    mockHasPermissionClaim.mockImplementation(
      (claim: string) => !claim.startsWith('Permissions.SystemSettings.'),
    )

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText(/Free tags are off/)).toBeInTheDocument()
  })

  it('says the setting could not be loaded rather than showing it as off', () => {
    // Arrange
    mockSettingsFailed = true

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(
      screen.getByText(
        'Could not load the product tag settings. Reload the page to try again.',
      ),
    ).toBeInTheDocument()
  })

  it('says the free tags could not be loaded rather than showing an empty list', () => {
    // Arrange
    mockSearchFailed = true

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(
      screen.getByText(
        'Could not load the free tags. Use refresh to try again.',
      ),
    ).toBeInTheDocument()
  })
})
