import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Editor } from '@tiptap/core'
import LinkButton from './link-button'
import {
  createTestEditor,
  selectText,
  stubEditorLayout,
} from './editor-test-utils'
import { toStoredMarkdown } from './stored-markdown'

beforeAll(stubEditorLayout)

describe('LinkButton', () => {
  let editor: Editor

  afterEach(() => editor.destroy())

  const markdown = () => toStoredMarkdown(editor.getMarkdown())

  const openDialog = async () => {
    await userEvent.click(screen.getByRole('button', { name: 'Link' }))
    return screen.findByRole('textbox', { name: 'Link address' })
  }

  it('links the selected text', async () => {
    // Arrange
    editor = createTestEditor('Read the guide today')
    selectText(editor, 'guide')
    render(<LinkButton editor={editor} />)
    const input = await openDialog()

    // Act
    await userEvent.type(input, 'wayd.dev/docs')
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }))

    // Assert
    expect(markdown()).toBe('Read the [guide](https://wayd.dev/docs) today')
  })

  it('inserts the address as its own link text when nothing is selected', async () => {
    // Arrange
    editor = createTestEditor('See ')
    editor.commands.focus('end')
    render(<LinkButton editor={editor} />)
    const input = await openDialog()

    // Act
    await userEvent.type(input, 'https://wayd.dev{Enter}')

    // Assert
    expect(markdown()).toBe('See [https://wayd.dev](https://wayd.dev)')
  })

  it('refuses an unsafe address and says why, changing nothing', async () => {
    // Arrange
    editor = createTestEditor('Read the guide')
    selectText(editor, 'guide')
    render(<LinkButton editor={editor} />)
    const input = await openDialog()

    // Act
    await userEvent.type(input, 'javascript:alert(1)')
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }))

    // Assert
    expect(screen.getByRole('alert')).toHaveTextContent('Use a web address')
    expect(markdown()).toBe('Read the guide')
  })

  it('shows the current address and removes the link', async () => {
    // Arrange
    editor = createTestEditor('Read the [guide](https://wayd.dev) today')
    selectText(editor, 'guide')
    render(<LinkButton editor={editor} />)
    const input = await openDialog()

    // Act
    const shown = (input as HTMLInputElement).value
    await userEvent.click(screen.getByRole('button', { name: 'Remove link' }))

    // Assert
    expect(shown).toBe('https://wayd.dev')
    expect(markdown()).toBe('Read the guide today')
  })

  it('writes an address with a space so markdown reads it back whole', async () => {
    // Arrange
    editor = createTestEditor('Read the guide')
    selectText(editor, 'guide')
    render(<LinkButton editor={editor} />)
    const input = await openDialog()

    // Act
    await userEvent.type(input, 'https://ex.com/my page')
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }))

    // Assert
    expect(markdown()).toBe('Read the [guide](https://ex.com/my%20page)')
  })
})
