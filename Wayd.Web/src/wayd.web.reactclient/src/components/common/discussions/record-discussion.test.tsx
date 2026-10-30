import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  ActivityCategory,
  ActivityLogDto,
  DiscussionDto,
  DiscussionEntryDto,
  DiscussionEntryType,
  DiscussionMessageDto,
  DiscussionMessageKind,
  EventActorKind,
} from '@/src/services/wayd-api'
import RecordDiscussion from './record-discussion'

const mockMessageError = jest.fn()
const mockMessageInfo = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockMessageError, info: mockMessageInfo }),
}))

jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown?: string }) => (
    <div data-testid="markdown">{markdown}</div>
  ),
}))

const mockUseDiscussion = jest.fn()
const mockUseEntries = jest.fn()
const mockPost = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  useGetDiscussionQuery: (...args: unknown[]) => mockUseDiscussion(...args),
  useGetDiscussionEntriesInfiniteQuery: (...args: unknown[]) =>
    mockUseEntries(...args),
  useGetDiscussionRepliesQuery: () => ({ data: [], isLoading: false }),
  useGetDiscussionRevisionsQuery: () => ({ data: [], isLoading: false }),
  usePostDiscussionMessageMutation: () => [mockPost, { isLoading: false }],
  useReplyToDiscussionMessageMutation: () => [jest.fn(), { isLoading: false }],
  useEditDiscussionMessageMutation: () => [jest.fn(), { isLoading: false }],
  useDeleteDiscussionMessageMutation: () => [jest.fn(), { isLoading: false }],
  useSetDiscussionLockMutation: () => [jest.fn(), { isLoading: false }],
}))

const author = { id: 'e-1', key: 1, name: 'Marcus Lee' }

const access = (overrides: Partial<DiscussionDto> = {}): DiscussionDto => ({
  canPost: true,
  canPostInternal: false,
  canModerate: false,
  isLocked: false,
  threadCount: 0,
  messageCount: 0,
  ...overrides,
})

const message = (
  overrides: Partial<DiscussionMessageDto> = {},
): DiscussionMessageDto => ({
  id: 'm-1',
  kind: DiscussionMessageKind.Public,
  body: 'Which fields does finance need?',
  author,
  postedOn: new Date(),
  isDeleted: false,
  mentions: [],
  replyCount: 0,
  recentRepliers: [],
  canEdit: false,
  canDelete: false,
  ...overrides,
})

const activity = (overrides: Partial<ActivityLogDto> = {}): ActivityLogDto => ({
  id: 7,
  eventType: 'IntakeRequestStatusChangedEvent',
  category: ActivityCategory.Updated,
  domainArea: 'DemandManagement',
  aggregateType: 'IntakeRequest',
  aggregateId: 'r-1',
  actorKind: EventActorKind.User,
  timestamp: new Date(),
  eventVersion: '1.0',
  payload: '{}',
  isRelated: false,
  ...overrides,
})

const showEntries = (items: DiscussionEntryDto[]) =>
  mockUseEntries.mockReturnValue({
    data: { pages: [{ items }] },
    isLoading: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: jest.fn(),
  })

const renderDiscussion = () =>
  render(
    <RecordDiscussion
      channelType="demand.intake-request"
      ownerId="r-1"
      audience={{
        [DiscussionMessageKind.Public]: 'Seen by everyone on this request',
        [DiscussionMessageKind.InternalNote]: "Only this desk's agents",
      }}
    />,
  )

describe('RecordDiscussion', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    window.localStorage.clear()
    mockUseDiscussion.mockReturnValue({ data: access(), isError: false })
    showEntries([])
    mockPost.mockResolvedValue({ data: 'm-2' })
  })

  it('offers no internal-note control to someone who cannot post notes', () => {
    // Act
    renderDiscussion()

    // Assert
    expect(screen.queryByText('Internal note')).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Post message' }),
    ).toBeInTheDocument()
  })

  it('switches the composer to an internal note for an agent', () => {
    // Arrange
    mockUseDiscussion.mockReturnValue({
      data: access({ canPostInternal: true }),
      isError: false,
    })
    renderDiscussion()

    // Act
    fireEvent.click(screen.getByText('Internal note'))

    // Assert
    expect(
      screen.getByRole('button', { name: 'Add internal note' }),
    ).toBeInTheDocument()
    expect(screen.getByText("Only this desk's agents")).toBeInTheDocument()
    expect(document.querySelector('[data-kind="InternalNote"]')).not.toBeNull()
  })

  it('posts what the composer holds as a reply', async () => {
    // Arrange
    renderDiscussion()

    // Act
    fireEvent.change(screen.getByLabelText('Subject'), {
      target: { value: '  Budget  ' },
    })
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Could you add the budget?' },
    })
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Post message' }))
    })

    // Assert
    expect(screen.getByLabelText('Message')).toHaveValue('')
    expect(mockPost).toHaveBeenCalledWith({
      channelType: 'demand.intake-request',
      ownerId: 'r-1',
      kind: DiscussionMessageKind.Public,
      subject: 'Budget',
      body: 'Could you add the budget?',
    })
  })

  it('shows a message, the change it was posted with, and the activity around it', () => {
    // Arrange
    showEntries([
      {
        type: DiscussionEntryType.Activity,
        timestamp: new Date(),
        activity: activity({ summary: 'Submitted the request', id: 6 }),
      },
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message({
          kind: DiscussionMessageKind.InternalNote,
          subject: 'Overlap',
          recordChange: activity({
            summary: 'Moved to Waiting on Requester',
          }),
        }),
      },
    ])

    // Act
    renderDiscussion()

    // Assert
    expect(screen.getByText(/Submitted the request/)).toBeInTheDocument()
    expect(screen.getByText('Overlap')).toBeInTheDocument()
    expect(
      screen.getByText('Moved to Waiting on Requester'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('article', { name: 'Internal note by Marcus Lee' }),
    ).toBeInTheDocument()
  })

  it('shows a deleted message as who deleted it, never its text', () => {
    // Arrange
    showEntries([
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message({
          isDeleted: true,
          body: undefined,
          deletedBy: { id: 'e-2', key: 2, name: 'Dana Ortiz' },
          deletedOn: new Date(),
        }),
      },
    ])

    // Act
    renderDiscussion()

    // Assert
    expect(
      screen.getByText(/Message by Marcus Lee deleted by Dana Ortiz/),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('markdown')).toBeNull()
  })

  it('summarizes a thread until it is opened', () => {
    // Arrange
    showEntries([
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message({
          replyCount: 2,
          lastReplyOn: new Date(),
          recentRepliers: [{ id: 'e-3', key: 3, name: 'Tom Becker' }],
        }),
      },
    ])

    // Act
    renderDiscussion()

    // Assert
    expect(screen.getByText(/2 replies · last reply/)).toBeInTheDocument()
  })

  it('puts the composer above the conversation when read newest first', async () => {
    // Arrange
    showEntries([
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message(),
      },
    ])
    renderDiscussion()

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /Oldest first/ }))
    })

    // Assert
    const composer = screen.getByLabelText('Message')
    const entry = screen.getByRole('article')
    expect(
      composer.compareDocumentPosition(entry) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    // The order is the reader's own, kept in their browser.
    expect(window.localStorage.setItem).toHaveBeenCalledWith(
      'wayd.discussion.order:v1',
      JSON.stringify('NewestFirst'),
    )
  })

  it('offers no composer in a locked conversation', () => {
    // Arrange
    mockUseDiscussion.mockReturnValue({
      data: access({ isLocked: true }),
      isError: false,
    })

    // Act
    renderDiscussion()

    // Assert
    expect(screen.getByText(/This conversation is locked/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Message')).toBeNull()
  })

  it('shows nothing when the conversation cannot be read', () => {
    // Arrange
    mockUseDiscussion.mockReturnValue({
      data: undefined,
      error: { status: 404 },
    })

    // Act
    const { container } = renderDiscussion()

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('says the discussion could not be loaded when the server fails', () => {
    // Arrange — anything but not found is a failure to show, not a conversation to hide
    mockUseDiscussion.mockReturnValue({
      data: undefined,
      error: { status: 500 },
      refetch: jest.fn(),
    })

    // Act
    renderDiscussion()

    // Assert
    expect(
      screen.getByText('The discussion could not be loaded.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })

  it('keeps a draft when the order is switched', async () => {
    // Arrange
    renderDiscussion()
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Half a thought' },
    })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /Oldest first/ }))
    })

    // Assert
    expect(screen.getByLabelText('Message')).toHaveValue('Half a thought')
  })

  it('says where a new post went when newer messages are not loaded yet', async () => {
    // Arrange
    mockUseEntries.mockReturnValue({
      data: { pages: [{ items: [] }] },
      isLoading: false,
      hasNextPage: true,
      isFetchingNextPage: false,
      fetchNextPage: jest.fn(),
    })
    renderDiscussion()
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Could you add the budget?' },
    })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Post message' }))
    })

    // Assert
    expect(mockMessageInfo).toHaveBeenCalledWith(
      expect.stringContaining('Show newer'),
    )
  })

  it('heads the conversation with how many threads and messages the caller can read', () => {
    // Arrange
    mockUseDiscussion.mockReturnValue({
      data: access({ threadCount: 2, messageCount: 4 }),
      isError: false,
    })

    // Act
    renderDiscussion()

    // Assert
    expect(
      screen.getByRole('heading', { name: 'Discussion' }),
    ).toBeInTheDocument()
    expect(screen.getByText('2 threads · 4 messages')).toBeInTheDocument()
  })

  it('says a conversation nobody has posted in has no messages', () => {
    // Act
    renderDiscussion()

    // Assert
    expect(screen.getByText('No messages yet')).toBeInTheDocument()
  })

  it("offers a message's actions in one menu, holding only what the caller may do", async () => {
    // Arrange
    showEntries([
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message({ id: 'm-own', canEdit: true, canDelete: true }),
      },
      {
        type: DiscussionEntryType.Message,
        timestamp: new Date(),
        message: message({ id: 'm-other', canEdit: false, canDelete: false }),
      },
    ])
    renderDiscussion()

    // Act
    const menus = screen.getAllByRole('button', { name: 'Message actions' })
    await act(async () => {
      fireEvent.click(menus[0])
    })

    // Assert
    expect(menus).toHaveLength(1)
    expect(await screen.findByText('Edit')).toBeInTheDocument()
    expect(screen.getByText('Delete')).toBeInTheDocument()
  })
})
