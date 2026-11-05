import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Editor } from '@tiptap/core'
import { Form } from 'antd'
import { stubEditorLayout } from './editor-test-utils'
import { MARKDOWN_VIEW_KEY } from './markdown-view-preference'
import WysiwygMarkdownEditor from './wysiwyg-markdown-editor'

beforeAll(stubEditorLayout)

// jest.setup.ts replaces localStorage with mocks; each test starts with nothing remembered.
beforeEach(() => {
  jest.mocked(window.localStorage.getItem).mockReset().mockReturnValue(null)
  jest.mocked(window.localStorage.setItem).mockReset()
})

/** The editable element, once Tiptap has mounted it. */
const findEditable = () => screen.findByRole('textbox')

/** Clicks a toolbar button by its accessible name. */
const clickToolbar = async (name: string) =>
  userEvent.click(screen.getByRole('button', { name }))

/** Switches between the document view and the markdown source. */
const toggleSource = () => clickToolbar('Markdown source')

/** The source view's textarea; the hidden document view is not a visible textbox. */
const sourceBox = () => screen.getByRole<HTMLTextAreaElement>('textbox')

/** The textarea's own value setter, which React's change tracking sees, unlike a plain assignment. */
const setTextareaValue = Object.getOwnPropertyDescriptor(
  HTMLTextAreaElement.prototype,
  'value',
)!.set!

/** Replaces the source view's text, as typing or pasting over all of it would. */
const replaceSource = (markdown: string) =>
  fireEvent.change(sourceBox(), { target: { value: markdown } })

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
    expect(screen.getByRole('status')).toHaveTextContent(
      'That change would make the saved text 8 characters, over the limit of 5.',
    )
  })

  it('allows shortening text that was loaded over maxLength', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor
        value="abcdef"
        onChange={onChange}
        maxLength={3}
      />,
    )
    const editable = await findEditable()

    // Act
    await userEvent.click(editable)
    await userEvent.keyboard('{Control>}a{/Control}{Backspace}')

    // Assert
    expect(onChange).toHaveBeenLastCalledWith('')
    expect(screen.queryByRole('status')).toBeNull()
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
    expect(onChange).toHaveBeenLastCalledWith('abc\n\n---')
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
      'This text uses footnotes, which the document view cannot keep.',
    )
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Editing it here removes that formatting. To keep it, edit in Markdown.',
    )
  })

  it('opens the markdown source from the warning, where the formatting is kept', async () => {
    // Arrange
    const original = 'Note[^1]\n\n[^1]: Source.'
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor
        value={original}
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Edit in Markdown' }),
    )

    // Assert
    expect(sourceBox()).toHaveValue(original)
    expect(sourceBox()).toHaveFocus()
    expect(screen.queryByRole('alert')).toBeNull()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('shows the same warning again on switching back, so it is not announced anew', async () => {
    // Arrange
    render(
      <WysiwygMarkdownEditor
        value={'Note[^1]\n\n[^1]: Source.'}
        maxLength={100}
      />,
    )
    await findEditable()
    const warning = screen.getByRole('alert')
    await userEvent.click(
      screen.getByRole('button', { name: 'Edit in Markdown' }),
    )

    // Act
    await toggleSource()

    // Assert
    expect(screen.getByRole('alert')).toBe(warning)
    expect(warning).toHaveTextContent('To keep it, edit in Markdown.')
  })

  it('points at the source view again once a source edit replaces an edited document', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor
        value="Press <kbd>Esc</kbd>"
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()
    await clickToolbar('Bulleted list')
    await toggleSource()
    replaceSource('Press <kbd>Esc</kbd> now')

    // Act
    await toggleSource()

    // Assert
    expect(screen.getByRole('alert')).toHaveTextContent(
      'To keep it, edit in Markdown.',
    )
  })

  it('advises undoing, not the source view, once a document edit has removed the formatting', async () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WysiwygMarkdownEditor
        value="Press <kbd>Esc</kbd>"
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()

    // Act
    await clickToolbar('Bulleted list')
    const afterEdit = screen.getByRole('alert').textContent
    const offeredAfterEdit = screen.queryByRole('button', {
      name: 'Edit in Markdown',
    })
    await clickToolbar('Undo')

    // Assert
    expect(afterEdit).toContain(
      'Your edit has removed that formatting. To keep it, undo your edits or close without saving.',
    )
    expect(offeredAfterEdit).toBeNull()
    expect(screen.getByRole('alert')).toHaveTextContent(
      'To keep it, edit in Markdown.',
    )
    expect(
      screen.getByRole('button', { name: 'Edit in Markdown' }),
    ).toBeInTheDocument()
  })

  it('keeps the warning after an edit has removed what it warns about', async () => {
    // Arrange
    const onChange = jest.fn()
    const { rerender } = render(
      <WysiwygMarkdownEditor
        value="Press <kbd>Esc</kbd>"
        onChange={onChange}
        maxLength={100}
      />,
    )
    await findEditable()

    // Act
    await clickToolbar('Bulleted list')
    const edited = onChange.mock.lastCall![0]
    rerender(
      <WysiwygMarkdownEditor
        value={edited}
        onChange={onChange}
        maxLength={100}
      />,
    )

    // Assert
    expect(edited).toBe('- Press Esc')
    expect(screen.getByRole('alert')).toHaveTextContent(
      'This text uses HTML the editor does not support (<kbd>)',
    )
  })

  it('loads the record again when the form resets after an edit', async () => {
    // Arrange
    const onChange = jest.fn()
    const { rerender } = render(
      <WysiwygMarkdownEditor value="one" onChange={onChange} maxLength={100} />,
    )
    const editable = await findEditable()
    await clickToolbar('Bulleted list')
    rerender(
      <WysiwygMarkdownEditor
        value="- one"
        onChange={onChange}
        maxLength={100}
      />,
    )

    // Act
    rerender(
      <WysiwygMarkdownEditor value="one" onChange={onChange} maxLength={100} />,
    )

    // Assert
    await waitFor(() => expect(editable.querySelector('li')).toBeNull())
    expect(editable).toHaveTextContent('one')
    expect(onChange).toHaveBeenCalledTimes(1)
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

  it('focuses the editor when its label is clicked', async () => {
    // Arrange
    render(
      <Form name="form">
        <Form.Item name="description" label="Description">
          <WysiwygMarkdownEditor maxLength={100} />
        </Form.Item>
      </Form>,
    )
    const editable = await screen.findByRole('textbox', { name: 'Description' })

    // Act
    await userEvent.click(screen.getByText('Description'))

    // Assert
    await waitFor(() => expect(editable).toHaveFocus())
  })

  it('reports required and invalid state from the form item', async () => {
    // Arrange / Act
    const { container } = render(
      <Form
        name="form"
        fields={[{ name: ['description'], errors: ['Too long.'] }]}
      >
        <Form.Item
          name="description"
          label="Description"
          rules={[{ required: true }]}
        >
          <WysiwygMarkdownEditor maxLength={100} />
        </Form.Item>
      </Form>,
    )
    const editable = await screen.findByRole('textbox', { name: 'Description' })

    // Assert
    expect(editable).toHaveAttribute('aria-invalid', 'true')
    expect(editable).toHaveAttribute('aria-required', 'true')
    // antd renders the help text itself, which jsdom's motion stubs leave out; the link to it is ours.
    expect(editable).toHaveAttribute(
      'aria-describedby',
      'form_description_help',
    )
    expect(container.querySelector('[class*="invalid"]')).not.toBeNull()
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

  describe('markdown source view', () => {
    it('shows the loaded text exactly and changes nothing when switched there and back', async () => {
      // Arrange: `*` bullets, which the serializer would write as `-`.
      const original = '* one\n* two'
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value={original}
          onChange={onChange}
          maxLength={100}
        />,
      )
      const editable = await findEditable()

      // Act
      await toggleSource()
      const shown = sourceBox().value
      await toggleSource()
      await toggleSource()
      const shownAgain = sourceBox().value
      await toggleSource()

      // Assert
      expect(shown).toBe(original)
      expect(shownAgain).toBe(original)
      expect(onChange).not.toHaveBeenCalled()
      expect(editable.querySelectorAll('li')).toHaveLength(2)
    })

    it('shows the serialized text after an edit in the document view', async () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value="one"
          onChange={onChange}
          maxLength={100}
        />,
      )
      await findEditable()
      await clickToolbar('Bulleted list')

      // Act
      await toggleSource()

      // Assert
      expect(sourceBox()).toHaveValue('- one')
      expect(onChange).toHaveBeenCalledTimes(1)
    })

    it('keeps the document and its undo history when switched back without a source edit', async () => {
      // Arrange
      const original = '* one\n* two'
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value={original}
          onChange={onChange}
          maxLength={100}
        />,
      )
      await findEditable()
      await clickToolbar('Numbered list')

      // Act
      await toggleSource()
      await toggleSource()
      await clickToolbar('Undo')

      // Assert: undoing every edit still reports the text as loaded.
      expect(onChange).toHaveBeenLastCalledWith(original)
    })

    it('reports a source edit as typed and keeps it exactly after switching back', async () => {
      // Arrange
      const typed = '* first\n* second'
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value="one"
          onChange={onChange}
          maxLength={100}
        />,
      )
      const editable = await findEditable()
      await toggleSource()

      // Act
      replaceSource(typed)
      await toggleSource()
      await toggleSource()

      // Assert
      expect(onChange).toHaveBeenCalledTimes(1)
      expect(onChange).toHaveBeenLastCalledWith(typed)
      expect(sourceBox()).toHaveValue(typed)
      expect(editable.querySelectorAll('li')).toHaveLength(2)
    })

    it('treats a source edit as the new loaded text, so undoing a later edit reports it unchanged', async () => {
      // Arrange
      const typed = '* first\n* second'
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value="one"
          onChange={onChange}
          maxLength={100}
        />,
      )
      await findEditable()
      await toggleSource()
      replaceSource(typed)
      await toggleSource()

      // Act
      await clickToolbar('Numbered list')
      const edited = onChange.mock.lastCall![0]
      await clickToolbar('Undo')

      // Assert
      expect(edited).toBe('1. first\n2. second')
      expect(onChange).toHaveBeenLastCalledWith(typed)
    })

    it('loads a new record while in the source view, without calling onChange', async () => {
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
      await toggleSource()
      replaceSource('First record, edited')
      rerender(
        <WysiwygMarkdownEditor
          value="First record, edited"
          onChange={onChange}
          maxLength={100}
        />,
      )

      // Act
      rerender(
        <WysiwygMarkdownEditor
          value="* Second record"
          onChange={onChange}
          maxLength={100}
        />,
      )

      // Assert
      await waitFor(() => expect(sourceBox()).toHaveValue('* Second record'))
      await toggleSource()
      expect(editable.querySelector('li')).toHaveTextContent('Second record')
      expect(onChange).toHaveBeenCalledTimes(1)
    })

    it('refuses a source edit that would make the markdown longer than maxLength', async () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor value="abc" onChange={onChange} maxLength={5} />,
      )
      await findEditable()
      await toggleSource()

      // Act
      replaceSource('abcdef')

      // Assert
      expect(sourceBox()).toHaveValue('abc')
      expect(onChange).not.toHaveBeenCalled()
      expect(screen.getByRole('status')).toHaveTextContent(
        'That change would make the saved text 6 characters, over the limit of 5.',
      )
    })

    it('allows a source edit up to maxLength', async () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor value="abc" onChange={onChange} maxLength={5} />,
      )
      await findEditable()
      await toggleSource()

      // Act
      await userEvent.type(sourceBox(), 'de')

      // Assert
      expect(onChange).toHaveBeenLastCalledWith('abcde')
      expect(screen.queryByRole('status')).toBeNull()
    })

    it('allows shortening source text that was loaded over maxLength', async () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value="abcdef"
          onChange={onChange}
          maxLength={3}
        />,
      )
      await findEditable()
      await toggleSource()

      // Act
      replaceSource('abcde')

      // Assert
      expect(onChange).toHaveBeenLastCalledWith('abcde')
      expect(screen.queryByRole('status')).toBeNull()
    })

    it('moves focus into the view the toggle opens', async () => {
      // Arrange
      render(<WysiwygMarkdownEditor value="one" maxLength={100} />)
      const editable = await findEditable()

      // Act
      await toggleSource()
      const source = sourceBox()
      const focusedInSource = document.activeElement
      await toggleSource()

      // Assert
      expect(focusedInSource).toBe(source)
      await waitFor(() => expect(editable).toHaveFocus())
    })

    it('takes the form item label and aria state in the source view', async () => {
      // Arrange
      render(
        <Form
          name="form"
          fields={[{ name: ['description'], errors: ['Too long.'] }]}
        >
          <Form.Item
            name="description"
            label="Description"
            rules={[{ required: true }]}
          >
            <WysiwygMarkdownEditor maxLength={100} />
          </Form.Item>
        </Form>,
      )
      await screen.findByRole('textbox', { name: 'Description' })

      // Act
      await toggleSource()

      // Assert
      const source = screen.getByRole('textbox', { name: 'Description' })
      expect(source.tagName).toBe('TEXTAREA')
      expect(source).toHaveAttribute('aria-invalid', 'true')
      expect(source).toHaveAttribute('aria-required', 'true')
      expect(source).toHaveAttribute(
        'aria-describedby',
        'form_description_help',
      )
      expect(document.querySelectorAll('#form_description')).toHaveLength(1)
    })

    it('focuses the source view when its label is clicked', async () => {
      // Arrange
      render(
        <Form name="form">
          <Form.Item name="description" label="Description">
            <WysiwygMarkdownEditor maxLength={100} />
          </Form.Item>
        </Form>,
      )
      await screen.findByRole('textbox', { name: 'Description' })
      await toggleSource()
      act(() => (document.activeElement as HTMLElement).blur())

      // Act
      await userEvent.click(screen.getByText('Description'))

      // Assert
      expect(screen.getByRole('textbox', { name: 'Description' })).toHaveFocus()
    })

    it('remembers the chosen view for the next editor', async () => {
      // Arrange
      render(<WysiwygMarkdownEditor value="one" maxLength={100} />)
      await findEditable()

      // Act
      await toggleSource()
      await toggleSource()

      // Assert
      expect(window.localStorage.setItem).toHaveBeenNthCalledWith(
        1,
        MARKDOWN_VIEW_KEY,
        'source',
      )
      expect(window.localStorage.setItem).toHaveBeenLastCalledWith(
        MARKDOWN_VIEW_KEY,
        'document',
      )
    })

    it('opens in the source view when that was the last view chosen', async () => {
      // Arrange
      jest.mocked(window.localStorage.getItem).mockReturnValue('source')
      const onChange = jest.fn()

      // Act
      render(
        <WysiwygMarkdownEditor
          value="* one"
          onChange={onChange}
          maxLength={100}
        />,
      )

      // Assert
      await waitFor(() =>
        expect(
          screen.getByRole('button', { name: 'Markdown source' }),
        ).toHaveAttribute('aria-pressed', 'true'),
      )
      expect(sourceBox()).toHaveValue('* one')
      expect(onChange).not.toHaveBeenCalled()
    })

    it('opens in the document view when browser storage cannot be read', async () => {
      // Arrange
      jest.mocked(window.localStorage.getItem).mockImplementation(() => {
        throw new Error('SecurityError')
      })

      // Act
      render(<WysiwygMarkdownEditor value="one" maxLength={100} />)

      // Assert
      expect(await findEditable()).toHaveAttribute('contenteditable', 'true')
    })

    it('starts a new undo history when a source edit is switched back', async () => {
      // Arrange: deleting the first paragraph leaves an undo step that would survive the replace.
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value={'one\n\ntwo\n\nthree'}
          onChange={onChange}
          maxLength={100}
        />,
      )
      const editable = await findEditable()
      const editor = (editable as HTMLElement & { editor: Editor }).editor
      act(() => {
        editor.commands.deleteRange({ from: 0, to: 5 })
      })
      await toggleSource()
      replaceSource('brand new')

      // Act
      await toggleSource()
      await userEvent.click(editable)
      await userEvent.keyboard('{Control>}z{/Control}')

      // Assert
      expect(onChange).toHaveBeenNthCalledWith(1, 'two\n\nthree')
      expect(screen.getByRole('button', { name: 'Undo' })).toBeDisabled()
      expect(editable).toHaveTextContent(/^brand new$/)
      expect(onChange).toHaveBeenLastCalledWith('brand new')
    })

    it('starts a new undo history when the form loads another record', async () => {
      // Arrange
      const onChange = jest.fn()
      const { rerender } = render(
        <WysiwygMarkdownEditor
          value={'one\n\ntwo'}
          onChange={onChange}
          maxLength={100}
        />,
      )
      const editable = await findEditable()
      await clickToolbar('Bulleted list')

      // Act
      rerender(
        <WysiwygMarkdownEditor
          value="record two"
          onChange={onChange}
          maxLength={100}
        />,
      )

      // Assert
      await waitFor(() => expect(editable).toHaveTextContent('record two'))
      expect(screen.getByRole('button', { name: 'Undo' })).toBeDisabled()
    })

    it('reports text loaded with CRLF line endings unchanged when an edit is typed back out', async () => {
      // Arrange
      const original = 'a\r\nb'
      const onChange = jest.fn()
      render(
        <WysiwygMarkdownEditor
          value={original}
          onChange={onChange}
          maxLength={100}
        />,
      )
      await findEditable()
      await toggleSource()

      // Act
      await userEvent.type(sourceBox(), 'x{Backspace}')

      // Assert
      expect(onChange).toHaveBeenNthCalledWith(1, 'a\nbx')
      expect(onChange).toHaveBeenLastCalledWith(original)
    })

    it('keeps the caret where it was when the limit refuses typing', async () => {
      // Arrange
      render(<WysiwygMarkdownEditor value="abc" maxLength={3} />)
      await findEditable()
      await toggleSource()

      // Act: types `x` after `a`, as a browser delivers it: value and caret first, then the event.
      const box = sourceBox()
      act(() => {
        setTextareaValue.call(box, 'axbc')
        box.setSelectionRange(2, 2)
        box.dispatchEvent(new Event('input', { bubbles: true }))
      })

      // Assert
      expect(box).toHaveValue('abc')
      expect(box.selectionStart).toBe(1)
      expect(screen.getByRole('status')).toHaveTextContent(
        'That change would make the saved text 4 characters',
      )
    })

    it('gives the field id and label back to the document view after the source view', async () => {
      // Arrange: opens in the source view, so the document never had them.
      jest.mocked(window.localStorage.getItem).mockReturnValue('source')
      render(
        <Form name="form">
          <Form.Item name="description" label="Description">
            <WysiwygMarkdownEditor maxLength={100} />
          </Form.Item>
        </Form>,
      )
      await screen.findByRole('button', { name: 'Markdown source' })

      // Act
      await toggleSource()

      // Assert
      const editable = await screen.findByRole('textbox', {
        name: 'Description',
      })
      expect(editable).toHaveAttribute('contenteditable', 'true')
      expect(editable).toHaveAttribute('id', 'form_description')
    })

    it('switches views when browser storage cannot be written', async () => {
      // Arrange
      jest.mocked(window.localStorage.setItem).mockImplementation(() => {
        throw new Error('QuotaExceededError')
      })
      render(<WysiwygMarkdownEditor value="one" maxLength={100} />)
      await findEditable()

      // Act
      await toggleSource()

      // Assert
      expect(sourceBox()).toHaveValue('one')
    })
  })
})
