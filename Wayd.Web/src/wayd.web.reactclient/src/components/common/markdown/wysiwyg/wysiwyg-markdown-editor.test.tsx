import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Form } from 'antd'
import WysiwygMarkdownEditor from './wysiwyg-markdown-editor'

// ProseMirror measures the selection to scroll it into view; jsdom has no layout to measure.
beforeAll(() => {
  const emptyRect = () => new DOMRect(0, 0, 0, 0)
  Range.prototype.getClientRects = () =>
    ({
      length: 0,
      item: () => null,
      [Symbol.iterator]: [][Symbol.iterator],
    }) as unknown as DOMRectList
  Range.prototype.getBoundingClientRect = emptyRect
  document.elementFromPoint = () => null
})

/** The editable element, once Tiptap has mounted it. */
const findEditable = () => screen.findByRole('textbox')

/** Clicks a toolbar button by its accessible name. */
const clickToolbar = async (name: string) =>
  userEvent.click(screen.getByRole('button', { name }))

/** Pastes `html` into `target` the way a browser delivers a clipboard paste. */
const pasteHtml = (target: Element, html: string) => {
  const clipboardData = {
    types: ['text/html', 'text/plain'],
    getData: (type: string) => (type === 'text/html' ? html : ''),
  }
  const event = new Event('paste', { bubbles: true, cancelable: true })
  Object.defineProperty(event, 'clipboardData', { value: clipboardData })
  act(() => {
    target.dispatchEvent(event)
  })
}

describe('WysiwygMarkdownEditor', () => {
  it('shows the loaded markdown as formatted text', async () => {
    // Arrange / Act
    render(<WysiwygMarkdownEditor value="Some **bold** text" maxLength={100} />)

    // Assert
    const editable = await findEditable()
    expect(editable.querySelector('strong')).toHaveTextContent('bold')
  })

  it('does not call onChange when content loads', async () => {
    // Arrange
    const onChange = jest.fn()

    // Act
    render(
      <WysiwygMarkdownEditor
        value="* one\n* two"
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()

    // Assert
    expect(onChange).not.toHaveBeenCalled()
  })

  it('loads a new value without calling onChange when the form resets or switches record', async () => {
    // Arrange
    const onChange = jest.fn()
    const { rerender } = render(
      <WysiwygMarkdownEditor
        value="First record"
        onChange={onChange}
        maxLength={100}
      />,
    )
    const editable = await findEditable()

    // Act
    rerender(
      <WysiwygMarkdownEditor
        value="Second record"
        onChange={onChange}
        maxLength={100}
      />,
    )

    // Assert
    await waitFor(() => expect(editable).toHaveTextContent('Second record'))
    expect(onChange).not.toHaveBeenCalled()
  })

  it('reports the edited document as markdown', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor value="one" onChange={onChange} maxLength={100} />,
    )
    await findEditable()

    // Act
    await clickToolbar('Bulleted list')

    // Assert
    expect(onChange).toHaveBeenLastCalledWith('- one')
  })

  it('reports the original text, not a rewrite, when an edit is undone', async () => {
    // Arrange: `*` bullets, which the serializer would write as `-`.
    const original = '* one\n* two'
    const onChange = jest.fn()
    const { rerender } = render(
      <WysiwygMarkdownEditor
        value={original}
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()
    await clickToolbar('Numbered list')
    const edited = onChange.mock.lastCall![0]
    // The form feeds the edit back in, as `Form.Item` does.
    rerender(
      <WysiwygMarkdownEditor
        value={edited}
        onChange={onChange}
        maxLength={100}
      />,
    )

    // Act
    await clickToolbar('Undo')

    // Assert
    expect(edited).toBe('1. one\n2. two')
    expect(onChange).toHaveBeenLastCalledWith(original)
  })

  it('refuses an edit that would make the markdown longer than maxLength', async () => {
    // Arrange: a rule adds `\n\n---`, taking `abc` to 8 characters.
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor value="abc" onChange={onChange} maxLength={5} />,
    )
    const editable = await findEditable()

    // Act
    await clickToolbar('Horizontal rule')

    // Assert
    expect(onChange).not.toHaveBeenCalled()
    expect(editable.querySelector('hr')).toBeNull()
  })

  it('allows the same edit within maxLength', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor value="abc" onChange={onChange} maxLength={8} />,
    )
    const editable = await findEditable()

    // Act
    await clickToolbar('Horizontal rule')

    // Assert
    expect(editable.querySelector('hr')).not.toBeNull()
    expect(onChange.mock.lastCall![0].length).toBeLessThanOrEqual(8)
  })

  it('counts the stored markdown in the footer', async () => {
    // Arrange / Act
    render(<WysiwygMarkdownEditor value="Some **bold** text" maxLength={100} />)
    await findEditable()

    // Assert: 18 characters of markdown for 14 visible.
    expect(screen.getByText('18 / 100')).toBeInTheDocument()
  })

  it('warns before editing text that holds formatting the editor cannot keep', async () => {
    // Arrange / Act
    render(
      <WysiwygMarkdownEditor
        value={'Note[^1]\n\n[^1]: Source.'}
        maxLength={100}
      />,
    )
    await findEditable()

    // Assert
    expect(screen.getByRole('alert')).toHaveTextContent(
      'This text uses footnotes, which this editor cannot keep.',
    )
  })

  it('shows no warning for text it keeps', async () => {
    // Arrange / Act
    render(<WysiwygMarkdownEditor value="- [ ] task" maxLength={100} />)
    await findEditable()

    // Assert
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('marks the toolbar toggle for the formatting at the cursor as pressed', async () => {
    // Arrange / Act
    render(<WysiwygMarkdownEditor value="- one" maxLength={100} />)
    await findEditable()

    // Assert
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Bulleted list' }),
      ).toHaveAttribute('aria-pressed', 'true'),
    )
    expect(
      screen.getByRole('button', { name: 'Numbered list' }),
    ).toHaveAttribute('aria-pressed', 'false')
  })

  it('takes its accessible name from the form item label', async () => {
    // Arrange / Act
    render(
      <Form name="form">
        <Form.Item name="description" label="Description">
          <WysiwygMarkdownEditor maxLength={100} />
        </Form.Item>
      </Form>,
    )

    // Assert
    expect(
      await screen.findByRole('textbox', { name: 'Description' }),
    ).toBeInTheDocument()
  })

  it('pastes Office HTML as clean markdown, keeping only what the sanitizer allows', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor value="" onChange={onChange} maxLength={1000} />,
    )
    const editable = await findEditable()
    const wordHtml = [
      '<p class="MsoNormal" style="margin:0;font-family:Calibri" onclick="alert(1)">',
      '<b><span style="font-size:12pt;color:red">Bold</span></b> and <i>italic</i>',
      ' <a href="javascript:alert(1)">bad</a> <a href="https://wayd.dev" style="color:blue">good</a>',
      '<o:p></o:p></p>',
      '<!--[if gte mso 9]><xml><o:OfficeDocumentSettings/></xml><![endif]-->',
      '<ul><li style="mso-list:l0">one</li></ul>',
      '<p><img src="file:///C:/Users/x/image001.png"><img src="data:image/png;base64,AAAA"></p>',
      '<iframe src="https://attacker.example"></iframe><script>alert(1)</script>',
    ].join('')

    // Act
    fireEvent.focus(editable)
    pasteHtml(editable, wordHtml)

    // Assert
    expect(onChange).toHaveBeenLastCalledWith(
      '**Bold** and *italic* bad [good](https://wayd.dev)\n\n- one',
    )
  })
})
