import { act, fireEvent, render, screen } from '@testing-library/react'
import FollowButton from './follow-button'

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

const mockUseGetFollowStatusQuery = jest.fn()
const mockFollow = jest.fn()
const mockUnfollow = jest.fn()
jest.mock('@/src/store/features/common/follows-api', () => ({
  useGetFollowStatusQuery: (...args: unknown[]) =>
    mockUseGetFollowStatusQuery(...args),
  useFollowMutation: () => [mockFollow, { isLoading: false }],
  useUnfollowMutation: () => [mockUnfollow, { isLoading: false }],
}))

const target = { ownerType: 'ppm.project' as const, ownerId: 'project-1' }

describe('FollowButton', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseFeatureFlag.mockReturnValue({ isEnabled: true, isLoading: false })
    mockUseAuth.mockReturnValue({ user: { employeeId: 'employee-1' } })
    mockUseGetFollowStatusQuery.mockReturnValue({
      data: { isFollowing: false },
      isLoading: false,
    })
    mockFollow.mockResolvedValue({ data: undefined })
    mockUnfollow.mockResolvedValue({ data: undefined })
  })

  it('follows the record when the person does not follow it', async () => {
    render(<FollowButton {...target} />)

    const button = screen.getByRole('button', { name: /follow/i })
    expect(button).toHaveTextContent('Follow')
    expect(button).toHaveAttribute('aria-pressed', 'false')

    await act(async () => {
      fireEvent.click(button)
    })

    expect(mockFollow).toHaveBeenCalledWith(target)
    expect(mockUnfollow).not.toHaveBeenCalled()
  })

  it('stops following when the person already follows the record', async () => {
    mockUseGetFollowStatusQuery.mockReturnValue({
      data: { isFollowing: true },
      isLoading: false,
    })

    render(<FollowButton {...target} />)

    const button = screen.getByRole('button', { name: /following/i })
    expect(button).toHaveAttribute('aria-pressed', 'true')

    await act(async () => {
      fireEvent.click(button)
    })

    expect(mockUnfollow).toHaveBeenCalledWith(target)
    expect(mockFollow).not.toHaveBeenCalled()
  })

  it('shows the server explanation when following fails', async () => {
    mockFollow.mockResolvedValue({
      error: { status: 403, detail: 'Your account is not linked.' },
    })

    render(<FollowButton {...target} />)

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /follow/i }))
    })

    expect(mockMessageError).toHaveBeenCalledWith('Your account is not linked.')
  })

  it('renders nothing and asks nothing while the following flag is off', () => {
    mockUseFeatureFlag.mockReturnValue({ isEnabled: false, isLoading: false })

    const { container } = render(<FollowButton {...target} />)

    expect(mockUseFeatureFlag).toHaveBeenCalledWith('following')
    expect(container).toBeEmptyDOMElement()
    expect(mockUseGetFollowStatusQuery).toHaveBeenCalledWith(target, {
      skip: true,
    })
  })

  it('renders nothing for an account with no employee', () => {
    mockUseAuth.mockReturnValue({ user: { employeeId: null } })

    const { container } = render(<FollowButton {...target} />)

    expect(container).toBeEmptyDOMElement()
    expect(mockUseGetFollowStatusQuery).toHaveBeenCalledWith(target, {
      skip: true,
    })
  })

  it('renders nothing when the record cannot be read', () => {
    mockUseGetFollowStatusQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: { status: 404 },
    })

    const { container } = render(<FollowButton {...target} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('holds its place, disabled, while the status loads', () => {
    mockUseGetFollowStatusQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    render(<FollowButton {...target} />)

    expect(screen.getByRole('button', { name: /follow/i })).toBeDisabled()
  })

  it('stays visible but disabled when the status fails for another reason', () => {
    mockUseGetFollowStatusQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: { status: 500 },
    })

    render(<FollowButton {...target} />)

    expect(screen.getByRole('button', { name: /follow/i })).toBeDisabled()
  })
})
