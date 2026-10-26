import { act, fireEvent, render, screen } from '@testing-library/react'
import MuteButton from './mute-button'

const mockUseFeatureFlag = jest.fn()
jest.mock('@/src/hooks', () => ({
  useFeatureFlag: (name: string) => mockUseFeatureFlag(name),
}))

const mockUseAuth = jest.fn()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => mockUseAuth(),
}))

const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockMessageError }),
}))

const mockUseGetRecordMuteStatusQuery = jest.fn()
const mockMute = jest.fn()
const mockUnmute = jest.fn()
jest.mock('@/src/store/features/common/notification-mutes-api', () => ({
  useGetRecordMuteStatusQuery: (...args: unknown[]) =>
    mockUseGetRecordMuteStatusQuery(...args),
  useMuteRecordMutation: () => [mockMute, { isLoading: false }],
  useUnmuteRecordMutation: () => [mockUnmute, { isLoading: false }],
}))

const target = { ownerType: 'ppm.project' as const, ownerId: 'project-1' }

describe('MuteButton', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseFeatureFlag.mockReturnValue({ isEnabled: true, isLoading: false })
    mockUseAuth.mockReturnValue({ user: { employeeId: 'employee-1' } })
    mockUseGetRecordMuteStatusQuery.mockReturnValue({
      data: { isMuted: false },
      isLoading: false,
    })
    mockMute.mockResolvedValue({ data: null })
    mockUnmute.mockResolvedValue({ data: null })
  })

  it('mutes the record when it is not muted', async () => {
    // Arrange
    render(<MuteButton {...target} />)
    const button = screen.getByRole('button', { name: 'Mute notifications' })
    expect(button).toHaveAttribute('aria-pressed', 'false')

    // Act
    await act(async () => {
      fireEvent.click(button)
    })

    // Assert
    expect(mockMute).toHaveBeenCalledWith(target)
    expect(mockUnmute).not.toHaveBeenCalled()
  })

  it('unmutes the record when it is muted', async () => {
    // Arrange
    mockUseGetRecordMuteStatusQuery.mockReturnValue({
      data: { isMuted: true },
      isLoading: false,
    })
    render(<MuteButton {...target} />)
    const button = screen.getByRole('button', { name: 'Mute notifications' })
    expect(button).toHaveAttribute('aria-pressed', 'true')

    // Act
    await act(async () => {
      fireEvent.click(button)
    })

    // Assert
    expect(mockUnmute).toHaveBeenCalledWith(target)
    expect(mockMute).not.toHaveBeenCalled()
  })

  it('says why a mute failed', async () => {
    // Arrange
    mockMute.mockResolvedValue({
      error: { status: 403, detail: 'Your account is not linked.' },
    })
    render(<MuteButton {...target} />)

    // Act
    await act(async () => {
      fireEvent.click(
        screen.getByRole('button', { name: 'Mute notifications' }),
      )
    })

    // Assert
    expect(mockMessageError).toHaveBeenCalledWith('Your account is not linked.')
  })

  it('renders nothing and asks nothing while the following flag is off', () => {
    // Arrange
    mockUseFeatureFlag.mockReturnValue({ isEnabled: false, isLoading: false })

    // Act
    const { container } = render(<MuteButton {...target} />)

    // Assert
    expect(mockUseFeatureFlag).toHaveBeenCalledWith('following')
    expect(container).toBeEmptyDOMElement()
    expect(mockUseGetRecordMuteStatusQuery).toHaveBeenCalledWith(target, {
      skip: true,
    })
  })

  it('renders nothing for an account with no employee', () => {
    // Arrange
    mockUseAuth.mockReturnValue({ user: { employeeId: null } })

    // Act
    const { container } = render(<MuteButton {...target} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing for a record the person cannot read', () => {
    // Arrange
    mockUseGetRecordMuteStatusQuery.mockReturnValue({
      isLoading: false,
      error: { status: 404 },
    })

    // Act
    const { container } = render(<MuteButton {...target} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('stays disabled when the status could not be loaded', () => {
    // Arrange
    mockUseGetRecordMuteStatusQuery.mockReturnValue({
      isLoading: false,
      error: { status: 500 },
    })

    // Act
    render(<MuteButton {...target} />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Mute notifications' }),
    ).toBeDisabled()
  })
})
