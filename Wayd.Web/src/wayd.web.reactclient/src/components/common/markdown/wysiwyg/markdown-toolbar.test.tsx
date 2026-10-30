import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Editor } from '@tiptap/core'
import MarkdownToolbar from './markdown-toolbar'
import {
  createTestEditor,
  selectText,
  stubEditorLayout,
} from './editor-test-utils'
import { toStoredMarkdown } from './stored-markdown'

beforeAll(stubEditorLayout)

describe('MarkdownToolbar', () => {
  let editor: Editor

  afterEach(() => editor.destroy())

  const markdown = () => toStoredMarkdown(editor.getMarkdown())

  const renderToolbar = (content: string, cursorOn: string) => {
    editor = createTestEditor(content)
    selectText(editor, cursorOn)
    render(<MarkdownToolbar editor={editor} />)
  }

  const chooseMenuItem = async (button: RegExp | string, item: string) => {
    await userEvent.click(screen.getByRole('button', { name: button }))
    await userEvent.click(await screen.findByRole('menuitem', { name: item }))
  }

  it.each([
    ['Heading 1', '# Title'],
    ['Heading 3', '### Title'],
  ])('sets the text style to %s', async (style, expected) => {
    // Arrange
    renderToolbar('Title', 'Title')

    // Act
    await chooseMenuItem(/^Text style/, style)

    // Assert
    expect(markdown()).toBe(expected)
  })

  it('returns a heading to normal text and names the current style', async () => {
    // Arrange
    renderToolbar('## Title', 'Title')
    const label = screen
      .getByRole('button', { name: /^Text style/ })
      .getAttribute('aria-label')

    // Act
    await chooseMenuItem(/^Text style/, 'Normal text')

    // Assert
    expect(label).toBe('Text style: Heading 2')
    expect(markdown()).toBe('Title')
  })

  it.each([
    ['Quote', '> line'],
    ['Code block', '```\nline\n```'],
    ['Task list', '- [ ] line'],
    ['Numbered list', '1. line'],
  ])('%s formats the line', async (name, expected) => {
    // Arrange
    renderToolbar('line', 'line')

    // Act
    await userEvent.click(screen.getByRole('button', { name }))

    // Assert
    expect(markdown()).toBe(expected)
  })

  it('inserts a three-by-three table with a header row', async () => {
    // Arrange
    renderToolbar('Intro', 'Intro')

    // Act
    await chooseMenuItem('Table', 'Insert table')

    // Assert
    const rows = [...editor.view.dom.querySelectorAll('table tr')]
    expect(rows).toHaveLength(3)
    expect(rows[0].querySelectorAll('th')).toHaveLength(3)
    expect(rows[1].querySelectorAll('td')).toHaveLength(3)
  })

  it.each([
    [
      'Add row below',
      '| A   | B   |\n| --- | --- |\n|     |     |\n| 1   | 2   |',
    ],
    [
      'Add column after',
      '| A   |     | B   |\n| --- | --- | --- |\n| 1   |     | 2   |',
    ],
    ['Delete row', '| A   | B   |\n| --- | --- |'],
  ])('%s changes the table at the cursor', async (item, expected) => {
    // Arrange
    renderToolbar('| A | B |\n| --- | --- |\n| 1 | 2 |', 'A')
    if (item === 'Delete row') selectText(editor, '1')

    // Act
    await chooseMenuItem('Table', item)

    // Assert
    expect(markdown()).toBe(expected)
  })

  it('is one tab stop, with arrow keys moving between buttons', () => {
    // Arrange
    renderToolbar('line', 'line')
    const textStyle = screen.getByRole('button', { name: /^Text style/ })
    const bold = screen.getByRole('button', { name: 'Bold' })
    const lastEnabled = screen.getByRole('button', { name: 'Horizontal rule' })
    const tabStops = () =>
      screen.getAllByRole('button').filter((button) => button.tabIndex === 0)
    const initialStops = tabStops()
    const press = (key: string) =>
      fireEvent.keyDown(document.activeElement!, { key })

    // Act
    textStyle.focus()
    press('ArrowRight')
    const afterRight = document.activeElement
    press('End')
    const afterEnd = document.activeElement
    press('ArrowRight')
    const afterWrap = document.activeElement
    press('ArrowLeft')

    // Assert
    expect(initialStops).toEqual([textStyle])
    expect(afterRight).toBe(bold)
    expect(afterEnd).toBe(lastEnabled)
    expect(afterWrap).toBe(textStyle)
    expect(document.activeElement).toBe(lastEnabled)
    expect(tabStops()).toEqual([lastEnabled])
  })
})
