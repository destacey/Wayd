import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import MoveIntakeRequestButton from './move-intake-request-button'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

const mockChangeStatus = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useGetIntakeRequestStatusOptionsQuery: () => ({
    data: [
      {
        id: 's-review',
        name: 'In Review',
        category: 'Proposed',
        alias: 0,
        needsReason: false,
        needsSurvivingRequest: false,
      },
      {
        id: 's-waiting',
        name: 'Waiting on Requester',
        category: 'Proposed',
        alias: 2,
        needsReason: false,
        needsSurvivingRequest: false,
      },
      {
        id: 's-declined',
        name: 'Declined',
        category: 'Removed',
        alias: 6,
        needsReason: true,
        needsSurvivingRequest: false,
      },
    ],
    isFetching: false,
  }),
  useChangeIntakeRequestStatusMutation: () => [
    mockChangeStatus,
    { isLoading: false },
  ],
}))

const request = { summary: { id: 'r-42' } } as any

const pick = async (name: string) => {
  await userEvent.click(screen.getByRole('button', { name: /Move to/ }))
  await userEvent.click(await screen.findByText(name))
}

describe('MoveIntakeRequestButton', () => {
  beforeEach(() => {
    mockChangeStatus.mockReset()
    mockChangeStatus.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('moves the request at once to a status that needs nothing more', async () => {
    // Arrange
    const onNeedsDetail = vi.fn()
    render(
      <MoveIntakeRequestButton
        request={request}
        onNeedsDetail={onNeedsDetail}
      />,
    )

    // Act
    await pick('In Review')

    // Assert
    expect(mockChangeStatus).toHaveBeenCalledWith({
      requestId: 'r-42',
      statusId: 's-review',
    })
    expect(onNeedsDetail).not.toHaveBeenCalled()
  })

  it('opens the status form on a status that needs a reason', async () => {
    // Arrange
    const onNeedsDetail = vi.fn()
    render(
      <MoveIntakeRequestButton
        request={request}
        onNeedsDetail={onNeedsDetail}
      />,
    )

    // Act
    await pick('Declined')

    // Assert
    expect(onNeedsDetail).toHaveBeenCalledWith('s-declined')
    expect(mockChangeStatus).not.toHaveBeenCalled()
  })

  it('opens the status form for Waiting on Requester, to ask the question', async () => {
    // Arrange
    const onNeedsDetail = vi.fn()
    render(
      <MoveIntakeRequestButton
        request={request}
        onNeedsDetail={onNeedsDetail}
      />,
    )

    // Act
    await pick('Waiting on Requester')

    // Assert
    expect(onNeedsDetail).toHaveBeenCalledWith('s-waiting')
    expect(mockChangeStatus).not.toHaveBeenCalled()
  })
})
