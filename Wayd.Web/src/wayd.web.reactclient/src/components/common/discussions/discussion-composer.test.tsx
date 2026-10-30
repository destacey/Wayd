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

  it('clears what was written, and offers to only once something is', () => {
    // Arrange
    render(
      <DiscussionComposer
        canPostInternal={false}
        showSubject
        mentionCandidates={[]}
        onSubmit={jest.fn()}
      />,
    )
    expect(screen.queryByRole('button', { name: 'Clear' })).toBeNull()
    fireEvent.change(screen.getByLabelText('Subject'), {
      target: { value: 'Budget' },
    })
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Could you add it?' },
    })

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Clear' }))

    // Assert
    expect(screen.getByLabelText('Subject')).toHaveValue('')
    expect(screen.getByLabelText('Message')).toHaveValue('')
    expect(screen.queryByRole('button', { name: 'Clear' })).toBeNull()
  })

  it.each([
    ['Ctrl+Enter', { ctrlKey: true }, 1],
    ['⌘+Enter', { metaKey: true }, 1],
    ['Enter alone, which starts a new line', {}, 0],
  ])('posts on %s only when asked', async (_, modifiers, posts) => {
    // Arrange
    const onSubmit = jest.fn().mockResolvedValue(true)
    render(
      <DiscussionComposer
        canPostInternal={false}
        showSubject={false}
        mentionCandidates={[]}
        onSubmit={onSubmit}
      />,
    )
    const box = screen.getByLabelText('Message')
    fireEvent.change(box, { target: { value: 'Could you add the budget?' } })

    // Act
    await act(async () => {
      fireEvent.keyDown(box, { key: 'Enter', ...modifiers })
    })

    // Assert
    expect(onSubmit).toHaveBeenCalledTimes(posts)
  })
})
