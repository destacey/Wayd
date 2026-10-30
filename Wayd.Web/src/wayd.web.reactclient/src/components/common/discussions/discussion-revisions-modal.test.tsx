import { render, screen } from '@testing-library/react'
import {
  DiscussionMessageDto,
  DiscussionMessageKind,
} from '@/src/services/wayd-api'
import { DiscussionContext } from './discussion-context'
import DiscussionRevisionsModal from './discussion-revisions-modal'

jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown?: string }) => (
    <div data-testid="version">{markdown}</div>
  ),
}))

const mockUseRevisions = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  useGetDiscussionRevisionsQuery: (...args: unknown[]) =>
    mockUseRevisions(...args),
}))

const message = {
  id: 'm-1',
  kind: DiscussionMessageKind.Public,
  mentions: [],
} as unknown as DiscussionMessageDto

describe('DiscussionRevisionsModal', () => {
  it('lists the earlier versions newest first, as the server returns them', () => {
    // Arrange
    mockUseRevisions.mockReturnValue({
      isLoading: false,
      data: [
        { body: 'Second draft', replacedOn: new Date('2026-10-29T10:05:00Z') },
        { body: 'First draft', replacedOn: new Date('2026-10-29T10:00:00Z') },
      ],
    })

    // Act
    render(
      <DiscussionContext.Provider
        value={{
          target: { channelType: 'demand.intake-request', ownerId: 'r-1' },
          access: {
            canPost: true,
            canPostInternal: false,
            canModerate: false,
            isLocked: false,
            threadCount: 0,
            messageCount: 0,
          },
          mentionCandidates: [],
        }}
      >
        <DiscussionRevisionsModal message={message} onClose={jest.fn()} />
      </DiscussionContext.Provider>,
    )

    // Assert
    expect(screen.getAllByTestId('version').map((v) => v.textContent)).toEqual([
      'Second draft',
      'First draft',
    ])
  })
})
