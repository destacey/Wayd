'use client'

import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { EditorContent, useEditor } from '@tiptap/react'
import { Alert, Button, Input, Typography } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import MarkdownEditorFooter from '../markdown-editor-footer'
import { markdownEditorExtensions } from './markdown-extensions'
import { setMarkdownLengthLimit } from './markdown-length-limit'
import MarkdownToolbar from './markdown-toolbar'
import {
  type MarkdownView,
  readMarkdownView,
  saveMarkdownView,
} from './markdown-view-preference'
import { toStoredMarkdown } from './stored-markdown'
import { findUnsupportedMarkdown } from './unsupported-markdown'
import styles from './wysiwyg-markdown-editor.module.css'

export interface WysiwygMarkdownEditorProps {
  /** Set by `Form.Item`; ties the editor to the item's label. */
  id?: string
  value?: string
  /** Called with the markdown to store, only when the user edits. */
  onChange?: (value: string) => void
  /** Limit on the stored markdown, which is longer than the text shown. */
  maxLength: number
  /** Set by `Form.Item` while the field shows validation help or errors. */
  'aria-describedby'?: string
  /** Set by `Form.Item` while the field is invalid; also turns the border to the error color. */
  'aria-invalid'?: boolean | 'true' | 'false'
  /** Set by `Form.Item` when the field is required. */
  'aria-required'?: boolean | 'true' | 'false'
}

/** Markdown the editor loaded, and the document it parsed to. */
interface LoadedContent {
  markdown: string
  document: ProseMirrorNode
}

const listFormatter = new Intl.ListFormat('en', { type: 'conjunction' })

/**
 * Document editor for markdown fields: formats as the user types and stores markdown. A toolbar
 * toggle shows the same field as its markdown source instead.
 *
 * While the document still equals the one loaded, it reports the loaded text unchanged rather
 * than re-serializing it, so opening and saving a record never rewrites what nobody edited.
 * Switching views reports nothing; text edited in the source view is parsed once on the way back
 * and becomes the loaded text, so it is stored exactly as typed until the document is edited.
 */
const WysiwygMarkdownEditor = ({
  id,
  value,
  onChange,
  maxLength,
  'aria-describedby': ariaDescribedBy,
  'aria-invalid': ariaInvalid,
  'aria-required': ariaRequired,
}: WysiwygMarkdownEditorProps) => {
  const loadedRef = useRef<LoadedContent | null>(null)
  // The markdown currently stored for the field: loaded, or last reported through `onChange`.
  const lastReportedRef = useRef<string | null>(null)
  // The text as loaded, for the warning: an edit removes what it warns about from `value`.
  const [loadedMarkdown, setLoadedMarkdown] = useState(value ?? '')
  // The length an edit would have reached when the limit last refused one; cleared by the next edit.
  const [refusedLength, setRefusedLength] = useState<number | null>(null)

  const [view, setView] = useState<MarkdownView>(readMarkdownView)
  const [sourceText, setSourceText] = useState(value ?? '')
  // What the source view showed when it opened or last loaded; text that differs was edited there.
  const sourceShownRef = useRef(value ?? '')
  const sourceRef = useRef<TextAreaRef>(null)
  // Set by the toggle, so the view it opens takes focus; a hidden view cannot keep it.
  const focusOnSwitchRef = useRef(false)

  // Created once: a new extensions array on each render makes Tiptap reconfigure the editor.
  const [extensions] = useState(markdownEditorExtensions)

  const isSourceView = view === 'source'
  const labelId = id ? `${id}-label` : undefined
  const isInvalid = !!ariaInvalid && ariaInvalid !== 'false'
  const isRequired = !!ariaRequired && ariaRequired !== 'false'

  const editor = useEditor({
    extensions,
    immediatelyRender: false,
    editorProps: {
      // The field's id and aria state belong to whichever view is shown.
      attributes: {
        class: styles.content,
        role: 'textbox',
        'aria-multiline': 'true',
        ...(id && !isSourceView ? { id } : {}),
        ...(labelId && !isSourceView ? { 'aria-labelledby': labelId } : {}),
        ...(ariaDescribedBy ? { 'aria-describedby': ariaDescribedBy } : {}),
        ...(isInvalid ? { 'aria-invalid': 'true' } : {}),
        ...(isRequired ? { 'aria-required': 'true' } : {}),
      },
    },
    onUpdate: ({ editor }) => {
      setRefusedLength(null)
      const loaded = loadedRef.current
      const markdown =
        loaded && editor.state.doc.eq(loaded.document)
          ? loaded.markdown
          : toStoredMarkdown(editor.getMarkdown())
      if (markdown === lastReportedRef.current) return
      lastReportedRef.current = markdown
      onChange?.(markdown)
    },
  })

  useEffect(() => {
    if (editor) setMarkdownLengthLimit(editor, maxLength, setRefusedLength)
  }, [editor, maxLength])

  // Loads `value` when it is not what the editor last reported: on mount, and when the form resets
  // or switches record. The echo of the user's own typing is skipped, so the cursor stays put.
  useEffect(() => {
    if (!editor) return
    const markdown = value ?? ''
    if (loadedRef.current && markdown === lastReportedRef.current) return

    editor
      .chain()
      .setMeta('addToHistory', false)
      .setContent(markdown, { contentType: 'markdown', emitUpdate: false })
      .run()
    loadedRef.current = { markdown, document: editor.state.doc }
    lastReportedRef.current = markdown
    sourceShownRef.current = markdown
    setSourceText(markdown)
    setLoadedMarkdown(markdown)
    setRefusedLength(null)
  }, [editor, value])

  // A label's `for` cannot point at a contenteditable, so the label is named instead, and a click
  // on it focuses the editor as it would an input. The source view's textarea needs neither.
  useEffect(() => {
    if (!editor || !id || !labelId || isSourceView) return
    const label = document.querySelector<HTMLLabelElement>(
      `label[for="${CSS.escape(id)}"]`,
    )
    if (!label) return
    const previousId = label.id
    label.id = labelId
    const focusEditor = (event: MouseEvent) => {
      event.preventDefault()
      editor.commands.focus()
    }
    label.addEventListener('click', focusEditor)
    return () => {
      label.removeEventListener('click', focusEditor)
      label.id = previousId
    }
  }, [editor, id, labelId, isSourceView])

  useEffect(() => {
    if (!focusOnSwitchRef.current) return
    focusOnSwitchRef.current = false
    if (isSourceView) sourceRef.current?.focus()
    else editor?.commands.focus()
  }, [editor, isSourceView])

  const showSource = () => {
    const markdown = lastReportedRef.current ?? value ?? ''
    sourceShownRef.current = markdown
    setSourceText(markdown)
  }

  // Parses text edited in the source view, once, as the new loaded text. Unedited, the document is
  // left as it was, with its undo history.
  const showDocument = () => {
    if (!editor || sourceText === sourceShownRef.current) return
    editor
      .chain()
      .setMeta('addToHistory', false)
      .setContent(sourceText, { contentType: 'markdown', emitUpdate: false })
      .run()
    loadedRef.current = { markdown: sourceText, document: editor.state.doc }
    lastReportedRef.current = sourceText
    sourceShownRef.current = sourceText
    setLoadedMarkdown(sourceText)
  }

  const switchView = (next: MarkdownView) => {
    if (next === view) return
    if (next === 'source') showSource()
    else showDocument()
    setRefusedLength(null)
    focusOnSwitchRef.current = true
    setView(next)
    saveMarkdownView(next)
  }

  // Held to the same rule as the document view: an edit past the limit is refused whole, unless it
  // shortens text that was already over.
  const onSourceChange = (event: ChangeEvent<HTMLTextAreaElement>) => {
    const markdown = event.target.value
    if (markdown.length > maxLength && markdown.length >= sourceText.length) {
      setRefusedLength(markdown.length)
      return
    }
    setRefusedLength(null)
    setSourceText(markdown)
    if (markdown === lastReportedRef.current) return
    lastReportedRef.current = markdown
    onChange?.(markdown)
  }

  const unsupported = findUnsupportedMarkdown(loadedMarkdown)

  return (
    <>
      {unsupported.length > 0 && !isSourceView && (
        <Alert
          className={styles.warning}
          type="warning"
          showIcon
          title={`This text uses ${listFormatter.format(unsupported)}, which the document view cannot keep.`}
          description="Editing it here removes that formatting. To keep it, edit in Markdown."
          action={
            <Button size="small" onClick={() => switchView('source')}>
              Edit in Markdown
            </Button>
          }
        />
      )}
      <div
        className={
          isInvalid ? `${styles.editor} ${styles.invalid}` : styles.editor
        }
      >
        {editor && (
          <MarkdownToolbar
            editor={editor}
            editorId={id}
            sourceView={isSourceView}
            onToggleSourceView={() =>
              switchView(isSourceView ? 'document' : 'source')
            }
          />
        )}
        <div hidden={isSourceView}>
          <EditorContent editor={editor} />
        </div>
        {isSourceView && (
          <Input.TextArea
            ref={sourceRef}
            id={id}
            className={styles.source}
            variant="borderless"
            autoSize={{ minRows: 6 }}
            spellCheck
            value={sourceText}
            onChange={onSourceChange}
            aria-describedby={ariaDescribedBy}
            aria-invalid={isInvalid || undefined}
            aria-required={isRequired || undefined}
          />
        )}
      </div>
      {refusedLength !== null && (
        <Typography.Text type="danger" role="status">
          That change would make the saved text {refusedLength} characters, over
          the limit of {maxLength}.
        </Typography.Text>
      )}
      <MarkdownEditorFooter
        currentLength={value?.length ?? 0}
        maxLength={maxLength}
      />
    </>
  )
}

export default WysiwygMarkdownEditor
