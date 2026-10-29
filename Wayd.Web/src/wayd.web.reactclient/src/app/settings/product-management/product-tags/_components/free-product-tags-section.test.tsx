import { render, screen } from '@testing-library/react'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import FreeProductTagsSection, {
  freeTagActionItems,
} from './free-product-tags-section'

const mockHasPermissionClaim = jest.fn()
let mockAllowsFreeTags: boolean | undefined = true

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

jest.mock('@/src/store/features/product-management/product-tags-api', () => ({
  useSearchProductTagsQuery: () => ({
    data: [tag],
    isLoading: false,
    refetch: jest.fn(),
  }),
}))

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useGetProductTagSettingsQuery: () => ({
    data:
      mockAllowsFreeTags === undefined
        ? undefined
        : { allowsFreeTags: mockAllowsFreeTags },
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
  })

  it('says free tags are on and links to the setting', () => {
    // Arrange / Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText(/Free tags are on/)).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Change the setting' }),
    ).toHaveAttribute('href', '/settings/system/product-tags')
  })

  it('says free tags are off when they are', () => {
    // Arrange
    mockAllowsFreeTags = false

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.getByText(/Free tags are off/)).toBeInTheDocument()
  })

  it('leaves out the setting and its link for a viewer who cannot see settings', () => {
    // Arrange
    mockHasPermissionClaim.mockImplementation(
      (claim: string) => claim !== 'Permissions.SystemSettings.View',
    )
    mockAllowsFreeTags = undefined

    // Act
    render(<FreeProductTagsSection />)

    // Assert
    expect(screen.queryByText(/Free tags are (on|off)/)).not.toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Change the setting' }),
    ).not.toBeInTheDocument()
  })
})
