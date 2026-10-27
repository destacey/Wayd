import { fireEvent, render, screen } from '@testing-library/react'
import { Dropdown } from 'antd'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import useIntakeChannelActions from './use-intake-channel-actions'

const mockHasPermissionClaim = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

// The dialogs only need to be identifiable, to assert which one an action opened.
jest.mock('./intake-channel-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./change-intake-channel-state-form', () => ({
  __esModule: true,
  default: ({ archive }: { archive: boolean }) => (
    <div>{archive ? 'archive' : 'restore'} dialog</div>
  ),
}))

const channel = (
  overrides: Partial<IntakeChannelDto> = {},
): IntakeChannelDto => ({
  id: 'c-1',
  name: 'Support',
  description: 'Raised from support cases.',
  order: 1,
  isActive: true,
  ...overrides,
})

const Harness = ({ target }: { target: IntakeChannelDto }) => {
  const { getActionItems, dialogs } = useIntakeChannelActions()
  const items = getActionItems(target)
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

const openMenu = () => {
  fireEvent.click(screen.getByRole('button', { name: /Actions/ }))
  return Array.from(document.querySelectorAll('.ant-dropdown-menu-item')).map(
    (item) => item.textContent,
  )
}

describe('useIntakeChannelActions', () => {
  beforeEach(() => {
    mockHasPermissionClaim.mockReset()
  })

  it('offers edit and archive on an active channel, and never delete', () => {
    // Arrange
    mockHasPermissionClaim.mockReturnValue(true)
    render(<Harness target={channel()} />)

    // Act
    const labels = openMenu()

    // Assert
    expect(labels).toEqual(['Edit', 'Archive'])
  })

  it('offers restore on an archived channel', () => {
    // Arrange
    mockHasPermissionClaim.mockReturnValue(true)
    render(<Harness target={channel({ isActive: false })} />)

    // Act
    const labels = openMenu()
    fireEvent.click(screen.getByText('Restore'))

    // Assert
    expect(labels).toEqual(['Edit', 'Restore'])
    expect(screen.getByText('restore dialog')).toBeInTheDocument()
  })

  it('offers nothing without the Demand Management Admin permission', () => {
    // Arrange
    mockHasPermissionClaim.mockReturnValue(false)

    // Act
    render(<Harness target={channel()} />)

    // Assert
    expect(
      screen.queryByRole('button', { name: /Actions/ }),
    ).not.toBeInTheDocument()
    expect(mockHasPermissionClaim).toHaveBeenCalledWith(
      'Permissions.IntakeRequests.Administer',
    )
  })
})
