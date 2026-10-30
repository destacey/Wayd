'use client'

import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { EditorContent, useEditor } from '@tiptap/react'
import { Alert } from 'antd'
import { useEffect, useRef, useState } from 'react'
import MarkdownEditorFooter from '../markdown-editor-footer'
import { markdownEditorExtensions } from './markdown-extensions'
import { setMarkdownMaxLength } from './markdown-length-limit'
import MarkdownToolbar from './markdown-toolbar'
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
}

/** Markdown the editor loaded, and the document it parsed to. */
interface LoadedContent {
  markdown: string
  document: ProseMirrorNode
}

const listFormatter = new Intl.ListFormat('en', { type: 'conjunction' })

/**
 * Document editor for markdown fields: formats as the user types and stores markdown.
 *
 * While the document still equals the one loaded, it reports the loaded text unchanged rather
 * than re-serializing it, so opening and saving a record never rewrites what nobody edited.
 */
const WysiwygMarkdownEditor = ({
  id,
  value,
  onChange,
  maxLength,
}: WysiwygMarkdownEditorProps) => {
  const loadedRef = useRef<LoadedContent | null>(null)
  const lastReportedRef = useRef<string | null>(null)

  // Created once: a new extensions array on each render makes Tiptap reconfigure the editor.
  const [extensions] = useState(markdownEditorExtensions)

  const labelId = id ? `${id}-label` : undefined

  const editor = useEditor({
    extensions,
    immediatelyRender: false,
    editorProps: {
      attributes: {
        class: styles.content,
        role: 'textbox',
        'aria-multiline': 'true',
        ...(id ? { id } : {}),
        ...(labelId ? { 'aria-labelledby': labelId } : {}),
      },
    },
    onUpdate: ({ editor }) => {
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
    if (editor) setMarkdownMaxLength(editor, maxLength)
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
  }, [editor, value])

  // A label's `for` cannot point at a contenteditable, so the label is named instead, and a click
  // on it focuses the editor as it would an input.
  useEffect(() => {
    if (!editor || !id || !labelId) return
    const label = document.querySelector<HTMLLabelElement>(
      `label[for="${CSS.escape(id)}"]`,
    )
    if (!label) return
    label.id = labelId
    const focusEditor = (event: MouseEvent) => {
      event.preventDefault()
      editor.commands.focus()
    }
    label.addEventListener('click', focusEditor)
    return () => label.removeEventListener('click', focusEditor)
  }, [editor, id, labelId])

  const unsupported = findUnsupportedMarkdown(value)

  return (
    <>
      {unsupported.length > 0 && (
        <Alert
          className={styles.warning}
          type="warning"
          showIcon
          title={`This text uses ${listFormatter.format(unsupported)}, which this editor cannot keep.`}
          description="Editing it removes that formatting. To keep it, close without saving."
        />
      )}
      <div className={styles.editor}>
        {editor && <MarkdownToolbar editor={editor} editorId={id} />}
        <EditorContent editor={editor} />
      </div>
      <MarkdownEditorFooter
        currentLength={value?.length ?? 0}
        maxLength={maxLength}
      />
    </>
  )
}

export default WysiwygMarkdownEditor
