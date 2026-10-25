import { fireEvent, render, screen } from '@testing-library/react'
import NotificationBell from './notification-bell'

const mockUseAuth = jest.fn()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => mockUseAuth(),
}))

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({ appBar: { backgroundColor: undefined, color: undefined } }),
}))

const mockUseFeatureFlag = jest.fn()
jest.mock('@/src/hooks', () => ({
  useFeatureFlag: (name: string) => mockUseFeatureFlag(name),
}))

const mockScreens = jest.fn()
jest.mock('antd', () => ({
  ...jest.requireActual('antd'),
  Grid: {
    ...jest.requireActual('antd').Grid,
    useBreakpoint: () => mockScreens(),
  },
}))

const mockUseCount = jest.fn()
jest.mock('@/src/store/features/common/notifications-api', () => ({
  ...jest.requireActual('@/src/store/features/common/notifications-api'),
  useGetUnreadNotificationCountQuery: (...args: unknown[]) =>
    mockUseCount(...args),
}))

jest.mock('./notification-panel', () => ({
  __esModule: true,
  default: ({
    onClose,
    inDrawer,
  }: {
    onClose: (options?: { returnFocus?: boolean }) => void
    inDrawer?: boolean
  }) => (
    <div data-testid="panel" data-in-drawer={inDrawer ? 'yes' : 'no'}>
      <button onClick={() => onClose({ returnFocus: true })}>Escape</button>
    </div>
  ),
}))

const arrange = ({
  flag = true,
  linked = true,
  unread = { count: 0, isCapped: false },
  phone = false,
} = {}) => {
  mockUseFeatureFlag.mockReturnValue({ isEnabled: flag, isLoading: false })
  mockUseAuth.mockReturnValue({
    user: { employeeId: linked ? 'emp-1' : undefined },
  })
  mockUseCount.mockReturnValue({ data: unread })
  mockScreens.mockReturnValue({ md: !phone })
}

describe('NotificationBell', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('is absent when following is off, and asks for no count', () => {
    // Arrange
    arrange({ flag: false })

    // Act
    render(<NotificationBell />)

    // Assert
    expect(mockUseFeatureFlag).toHaveBeenCalledWith('following')
    expect(screen.queryByRole('button', { name: /Notifications/ })).toBeNull()
    expect(mockUseCount).not.toHaveBeenCalled()
  })

  it('is absent when the account has no linked employee, and asks for no count', () => {
    // Arrange
    arrange({ linked: false })

    // Act
    render(<NotificationBell />)

    // Assert
    expect(screen.queryByRole('button', { name: /Notifications/ })).toBeNull()
    expect(mockUseCount).not.toHaveBeenCalled()
  })

  it('polls the count while the window has focus and refetches on focus', () => {
    // Arrange
    arrange()

    // Act
    render(<NotificationBell />)

    // Assert
    expect(mockUseCount).toHaveBeenCalledWith(undefined, {
      pollingInterval: 60_000,
      skipPollingIfUnfocused: true,
      refetchOnFocus: true,
    })
  })

  it('shows the unread count', () => {
    // Arrange
    arrange({ unread: { count: 7, isCapped: false } })

    // Act
    render(<NotificationBell />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Notifications, 7 unread' }),
    ).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it('shows 99+ when the server caps the count', () => {
    // Arrange
    arrange({ unread: { count: 99, isCapped: true } })

    // Act
    render(<NotificationBell />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Notifications, 99+ unread' }),
    ).toBeInTheDocument()
    expect(screen.getByText('99+')).toBeInTheDocument()
  })

  it('shows no count when nothing is unread', () => {
    // Arrange
    arrange()

    // Act
    render(<NotificationBell />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Notifications' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('0')).toBeNull()
  })

  it('opens the panel from the bell and says it is open', async () => {
    // Arrange
    arrange()
    render(<NotificationBell />)
    const bell = screen.getByRole('button', { name: 'Notifications' })
    expect(bell).toHaveAttribute('aria-expanded', 'false')

    // Act
    fireEvent.click(bell)

    // Assert
    expect(await screen.findByTestId('panel')).toHaveAttribute(
      'data-in-drawer',
      'no',
    )
    expect(bell).toHaveAttribute('aria-expanded', 'true')
  })

  it('hands focus back to the bell when the panel closes from the keyboard', async () => {
    // Arrange
    arrange()
    render(<NotificationBell />)
    const bell = screen.getByRole('button', { name: 'Notifications' })
    fireEvent.click(bell)
    await screen.findByTestId('panel')

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Escape' }))

    // Assert
    expect(bell).toHaveFocus()
    expect(bell).toHaveAttribute('aria-expanded', 'false')
  })

  it('opens the panel in a named drawer on a phone', async () => {
    // Arrange
    arrange({ phone: true })
    render(<NotificationBell />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Notifications' }))

    // Assert
    expect(
      await screen.findByRole('dialog', { name: 'Notifications' }),
    ).toBeInTheDocument()
    expect(screen.getByTestId('panel')).toHaveAttribute('data-in-drawer', 'yes')
  })
})
