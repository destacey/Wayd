import React, { ReactNode } from 'react'
import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  DiscussionMessageKind,
  IntakeDeskVisibility,
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import type {
  DiscussionComposerValue,
  MentionCandidate,
} from '@/src/components/common/discussions'
import IntakeRequestDiscussion, {
  WAITING_ON_REQUESTER,
} from './intake-request-discussion'

const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockMessageError }),
}))

interface CapturedProps {
  mentionCandidates: MentionCandidate[]
  noteMentionCandidates: MentionCandidate[]
  replyExtra?: ReactNode
  onPost: (value: DiscussionComposerValue) => Promise<boolean>
}
let captured: CapturedProps
jest.mock('@/src/components/common/discussions', () => ({
  RecordDiscussion: (props: CapturedProps) => {
    captured = props
    return <div>{props.replyExtra}</div>
  },
}))

const mockPost = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  DiscussionChannelTypes: { IntakeRequest: 'demand.intake-request' },
  usePostDiscussionMessageMutation: () => [mockPost, { isLoading: false }],
}))

const mockChangeStatus = jest.fn()
const mockUseDesk = jest.fn()
const mockUseEveryone = jest.fn()
const mockUseStatusOptions = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskQuery: (...args: unknown[]) => mockUseDesk(...args),
    useGetIntakeEmployeeOptionsQuery: (...args: unknown[]) =>
      mockUseEveryone(...args),
    useGetIntakeRequestStatusOptionsQuery: (...args: unknown[]) =>
      mockUseStatusOptions(...args),
    useChangeIntakeRequestStatusMutation: () => [
      mockChangeStatus,
      { isLoading: false },
    ],
  }),
)

const agent = { id: 'e-9', key: 9, name: 'Marcus Lee' }
const requester = { id: 'e-1', key: 1, name: 'Priya Natarajan' }

const substance = {
  submittedBy: requester,
  participants: [
    {
      employee: { id: 'e-3', key: 3, name: 'Tom Becker' },
      addedOn: new Date(),
    },
  ],
} as unknown as IntakeRequestSubstanceDto

const request = (overrides: Partial<IntakeRequestDetailsDto> = {}) =>
  ({
    summary: { id: 'r-1', key: 1, requester, desk: { id: 'd-1', key: 1 } },
    substance,
    canChangeStatus: true,
    canAddParticipants: false,
    ...overrides,
  }) as unknown as IntakeRequestDetailsDto

const waiting = {
  id: 's-waiting',
  name: 'Waiting on Requester',
  category: StatusCategory.Proposed,
  alias: WAITING_ON_REQUESTER,
  needsReason: false,
  needsSurvivingRequest: false,
}

const reply: DiscussionComposerValue = {
  kind: DiscussionMessageKind.Public,
  subject: 'Budget',
  body: 'Could you add the budget?',
}

describe('IntakeRequestDiscussion', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseDesk.mockReturnValue({
      data: {
        visibility: IntakeDeskVisibility.Private,
        members: [{ employee: agent }],
      },
    })
    mockUseEveryone.mockReturnValue({ data: undefined })
    mockUseStatusOptions.mockReturnValue({ data: [waiting] })
    mockPost.mockResolvedValue({ data: 'm-1' })
    mockChangeStatus.mockResolvedValue({ data: undefined })
  })

  it('posts a reply with the move to Waiting on Requester when the agent asks for it', async () => {
    // Arrange
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )
    fireEvent.click(screen.getByRole('checkbox'))

    // Act
    let posted = false
    await act(async () => {
      posted = await captured.onPost(reply)
    })

    // Assert
    expect(posted).toBe(true)
    expect(mockChangeStatus).toHaveBeenCalledWith({
      requestId: 'r-1',
      statusId: 's-waiting',
      reply: { subject: 'Budget', body: 'Could you add the budget?' },
    })
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('posts an ordinary reply without moving the request by default', async () => {
    // Arrange
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )

    // Act
    await act(async () => {
      await captured.onPost(reply)
    })

    // Assert
    expect(screen.getByRole('checkbox')).not.toBeChecked()
    expect(mockPost).toHaveBeenCalledWith({
      channelType: 'demand.intake-request',
      ownerId: 'r-1',
      ...reply,
    })
    expect(mockChangeStatus).not.toHaveBeenCalled()
  })

  it('never moves the request for an internal note', async () => {
    // Arrange
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )
    fireEvent.click(screen.getByRole('checkbox'))

    // Act
    await act(async () => {
      await captured.onPost({
        ...reply,
        kind: DiscussionMessageKind.InternalNote,
      })
    })

    // Assert
    expect(mockChangeStatus).not.toHaveBeenCalled()
    expect(mockPost).toHaveBeenCalled()
  })

  it('offers no move to someone who cannot change the status', () => {
    // Arrange
    mockUseStatusOptions.mockReturnValue({ data: undefined })

    // Act
    render(
      <IntakeRequestDiscussion
        request={request({ canChangeStatus: false })}
        substance={substance}
      />,
    )

    // Assert
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(mockUseStatusOptions).toHaveBeenCalledWith('r-1', { skip: true })
  })

  it('offers the request and its desk for mentions on a private desk, and only the desk in a note', () => {
    // Act
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )

    // Assert
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Priya Natarajan',
      'Tom Becker',
      'Marcus Lee',
    ])
    expect(captured.noteMentionCandidates.map((c) => c.name)).toEqual([
      'Marcus Lee',
    ])
    expect(mockUseEveryone).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('offers anyone for mentions where the author could add them on an open desk', () => {
    // Arrange
    mockUseDesk.mockReturnValue({
      data: { visibility: IntakeDeskVisibility.Open, members: [] },
    })
    mockUseEveryone.mockReturnValue({
      data: [{ id: 'e-7', key: 7, name: 'Dana Ortiz' }],
    })

    // Act
    render(
      <IntakeRequestDiscussion
        request={request({ canAddParticipants: true })}
        substance={substance}
      />,
    )

    // Assert
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Dana Ortiz',
    ])
  })

  it('says why a post failed and keeps the composer', async () => {
    // Arrange
    mockPost.mockResolvedValue({ error: { status: 400, detail: 'Locked.' } })
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )

    // Act
    let posted = true
    await act(async () => {
      posted = await captured.onPost(reply)
    })

    // Assert
    expect(posted).toBe(false)
    expect(mockMessageError).toHaveBeenCalledWith('Locked.')
  })
})
