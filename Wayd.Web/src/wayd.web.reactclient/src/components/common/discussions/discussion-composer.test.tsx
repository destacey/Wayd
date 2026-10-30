import { act, fireEvent, render, screen } from '@testing-library/react'
import { DiscussionMessageKind } from '@/src/services/wayd-api'
import DiscussionComposer from './discussion-composer'

const agent = { id: '01a18a77-484d-790c-ba91-93acff2826c1', name: 'Marcus Lee' }
const participant = {
  id: '01a18a77-484d-790c-ba91-93acff2826c2',
  name: 'Tom Becker',
}

describe('DiscussionComposer', () => {
  it('writes only the note audience as mentions in an internal note', async () => {
    // Arrange — someone picked while it was a reply, who cannot read notes
    const onSubmit = jest.fn().mockResolvedValue(true)
    render(
      <DiscussionComposer
        canPostInternal
        fixedKind={DiscussionMessageKind.InternalNote}
        showSubject={false}
        mentionCandidates={[agent, participant]}
        noteMentionCandidates={[agent]}
        initial={{
          text: '@Marcus Lee and @Tom Becker',
          mentioned: [agent, participant],
        }}
        submitLabel={() => 'Save'}
        onSubmit={onSubmit}
      />,
    )

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    // Assert
    expect(onSubmit).toHaveBeenCalledWith({
      kind: DiscussionMessageKind.InternalNote,
      subject: undefined,
      body: `@[Marcus Lee](employee:${agent.id}) and @Tom Becker`,
    })
  })
})
