'use client'

import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { EditorContent, useEditor } from '@tiptap/react'
import { Alert, Button, Input, Typography } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import {
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type ClipboardEvent,
  type DragEvent,
} from 'react'
import { errorDetail } from '@/src/utils/problem-details'
import { imageFilesIn, imageUploadProblem, uploadImage } from '../attachments'
import MarkdownEditorFooter from '../markdown-editor-footer'
import { loadMarkdown } from './load-markdown'
import {
  markdownEditorExtensions,
  markdownEditorPasteRules,
} from './markdown-extensions'
import {
  restoreRefusedInput,
  setMarkdownLengthLimit,
} from './markdown-length-limit'
import { setMarkdownImageUpload } from './markdown-image-upload'
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
  /**
   * Lines the editor shows while empty. When set, it grows with its content instead of
   * scrolling past a fixed height.
   */
  rows?: number
  /**
   * Whether images pasted or dropped into the field are uploaded and placed in it. Only for a
   * field whose record claims its images when it is saved; elsewhere they would be deleted a day
   * later.
   */
  imageUploads?: boolean
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

/** `markdown` with CRLF and CR line endings as LF, which is all a textarea's value holds. */
const toLf = (markdown: string) => markdown.replace(/\r\n?/g, '\n')

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
  rows,
  imageUploads = false,
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
  // Whether the document differs from the one loaded, so what the warning names is already gone.
  const [isDocumentEdited, setIsDocumentEdited] = useState(false)

  const [view, setView] = useState<MarkdownView>(readMarkdownView)
  const [sourceText, setSourceText] = useState(value ?? '')
  // What the source view showed when it opened or last loaded; text that differs was edited there.
  const sourceShownRef = useRef(value ?? '')
  const sourceRef = useRef<TextAreaRef>(null)
  // The source view's text as its textarea holds it (LF), for writing an upload in once it answers.
  const sourceTextRef = useRef(toLf(value ?? ''))
  // Where each image pasted into the source view and still uploading goes, kept in step with edits.
  const pendingSourceImagesRef = useRef<{ offset: number }[]>([])
  // The view shown now, for an upload that answers after a switch.
  const viewRef = useRef(view)
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
    enablePasteRules: markdownEditorPasteRules,
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
      const isUnedited = !!loaded && editor.state.doc.eq(loaded.document)
      setIsDocumentEdited(!isUnedited)
      const markdown = isUnedited
        ? loaded.markdown
        : toStoredMarkdown(editor.getMarkdown())
      if (markdown === lastReportedRef.current) return
      lastReportedRef.current = markdown
      onChange?.(markdown)
    },
  })

  // Why the last image pasted or dropped could not be added; cleared by the next edit.
  const [imageError, setImageError] = useState<string | null>(null)

  useEffect(() => {
    if (editor) setMarkdownLengthLimit(editor, maxLength, setRefusedLength)
  }, [editor, maxLength])

  useEffect(() => {
    if (!editor) return
    setMarkdownImageUpload(
      editor,
      imageUploads ? uploadImage : undefined,
      setImageError,
    )
  }, [editor, imageUploads])

  // Loads `value` when it is not what the editor last reported: on mount, and when the form resets
  // or switches record. The echo of the user's own typing is skipped, so the cursor stays put.
  useEffect(() => {
    if (!editor) return
    const markdown = value ?? ''
    if (loadedRef.current && markdown === lastReportedRef.current) return

    loadedRef.current = { markdown, document: loadMarkdown(editor, markdown) }
    lastReportedRef.current = markdown
    sourceShownRef.current = markdown
    sourceTextRef.current = toLf(markdown)
    setSourceText(markdown)
    setLoadedMarkdown(markdown)
    setIsDocumentEdited(false)
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
    sourceTextRef.current = toLf(markdown)
    pendingSourceImagesRef.current = []
    setSourceText(markdown)
  }

  // Parses text edited in the source view, once, as the new loaded text: the text reported for it,
  // which keeps the line endings it was loaded with. Unedited, the document is left as it was, with
  // its undo history.
  const showDocument = () => {
    if (!editor || sourceText === sourceShownRef.current) return
    const markdown = lastReportedRef.current ?? sourceText
    loadedRef.current = { markdown, document: loadMarkdown(editor, markdown) }
    sourceShownRef.current = markdown
    setLoadedMarkdown(markdown)
    setIsDocumentEdited(false)
  }

  const switchView = (next: MarkdownView) => {
    if (next === view) return
    if (next === 'source') showSource()
    else showDocument()
    setRefusedLength(null)
    focusOnSwitchRef.current = true
    viewRef.current = next
    setView(next)
    saveMarkdownView(next)
  }

  // Held to the same rule as the document view: an edit past the limit is refused whole, unless it
  // shortens text that was already over.
  const onSourceChange = (event: ChangeEvent<HTMLTextAreaElement>) => {
    const text = event.target.value
    if (text.length > maxLength && text.length >= sourceText.length) {
      // antd passes a copy of the textarea as `event.target`, so the element comes from the ref.
      const textarea = sourceRef.current?.resizableTextArea?.textArea
      if (textarea) restoreRefusedInput(textarea, sourceText)
      setRefusedLength(text.length)
      return
    }
    applySourceText(text)
  }

  // An image pasted or dropped as markdown source is uploaded, then written as `![](path)` where it
  // was placed, wherever later edits have moved that place. One that answers after a switch to the
  // document goes at the document's end, so it is never lost. As in the document view, a paste
  // that also carries HTML is left to the textarea.
  const uploadIntoSource = (files: File[], offset: number) => {
    setImageError(null)
    for (const file of files) {
      const problem = imageUploadProblem(file)
      if (problem) {
        setImageError(problem)
        continue
      }
      const pending = { offset }
      pendingSourceImagesRef.current.push(pending)
      uploadImage(file).then(
        ({ path }) => {
          const pendingImages = pendingSourceImagesRef.current
          if (!pendingImages.includes(pending)) return
          pendingSourceImagesRef.current = pendingImages.filter(
            (p) => p !== pending,
          )
          const image = `![](${path})`
          if (viewRef.current !== 'source') {
            editor?.commands.insertContentAt(editor.state.doc.content.size, {
              type: 'paragraph',
              content: [{ type: 'image', attrs: { src: path, alt: '' } }],
            })
            return
          }
          const current = sourceTextRef.current
          const at = Math.min(pending.offset, current.length)
          const text = `${current.slice(0, at)}${image}${current.slice(at)}`
          if (text.length > maxLength) {
            setRefusedLength(text.length)
            return
          }
          applySourceText(text)
        },
        (error: unknown) => {
          pendingSourceImagesRef.current =
            pendingSourceImagesRef.current.filter((p) => p !== pending)
          setImageError(
            errorDetail(error) ?? 'The image could not be uploaded.',
          )
        },
      )
    }
  }

  const onSourcePaste = (event: ClipboardEvent<HTMLTextAreaElement>) => {
    const files = imageFilesIn(event.clipboardData)
    if (!imageUploads || files.length === 0) return
    if (event.clipboardData.types.includes('text/html')) return
    event.preventDefault()
    uploadIntoSource(files, event.currentTarget.selectionStart)
  }

  const onSourceDrop = (event: DragEvent<HTMLTextAreaElement>) => {
    const files = imageFilesIn(event.dataTransfer)
    if (!imageUploads || files.length === 0) return
    event.preventDefault()
    uploadIntoSource(files, event.currentTarget.selectionStart)
  }

  // Moves each pending upload's place past an edit before it, so the image lands where it was put.
  const shiftPendingSourceImages = (previous: string, next: string) => {
    let start = 0
    while (
      start < previous.length &&
      start < next.length &&
      previous[start] === next[start]
    )
      start++
    const delta = next.length - previous.length
    for (const pending of pendingSourceImagesRef.current)
      if (pending.offset > start)
        pending.offset = Math.max(start, pending.offset + delta)
  }

  const applySourceText = (text: string) => {
    setRefusedLength(null)
    shiftPendingSourceImages(sourceTextRef.current, text)
    sourceTextRef.current = text
    setSourceText(text)
    // Text loaded with CRLF line endings comes back from the textarea with LF; typed back to what
    // was loaded, it reports the loaded text rather than rewriting every line ending.
    const loaded = loadedRef.current?.markdown
    const markdown =
      loaded !== undefined && toLf(loaded) === text ? loaded : text
    if (markdown === lastReportedRef.current) return
    lastReportedRef.current = markdown
    onChange?.(markdown)
  }

  const unsupported = findUnsupportedMarkdown(loadedMarkdown)

  return (
    <>
      {/* Hidden rather than unmounted in the source view, so a screen reader does not announce the
          warning again on each switch back. */}
      {unsupported.length > 0 && (
        <div hidden={isSourceView}>
          <Alert
            className={styles.warning}
            type="warning"
            showIcon
            title={`This text uses ${listFormatter.format(unsupported)}, which the document view cannot keep.`}
            description={
              isDocumentEdited
                ? 'Your edit has removed that formatting. To keep it, undo your edits or close without saving.'
                : 'Editing it here removes that formatting. To keep it, edit in Markdown.'
            }
            action={
              isDocumentEdited ? undefined : (
                <Button size="small" onClick={() => switchView('source')}>
                  Edit in Markdown
                </Button>
              )
            }
          />
        </div>
      )}
      <div
        className={
          isInvalid ? `${styles.editor} ${styles.invalid}` : styles.editor
        }
        style={
          rows
            ? ({
                '--editor-min-height': `calc(${rows} * var(--ant-font-size) * var(--ant-line-height) + 2 * var(--ant-padding-xs))`,
                '--editor-max-height': 'none',
              } as React.CSSProperties)
            : undefined
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
            autoSize={{ minRows: rows ?? 6 }}
            spellCheck
            value={sourceText}
            onChange={onSourceChange}
            onPaste={onSourcePaste}
            onDrop={onSourceDrop}
            aria-describedby={ariaDescribedBy}
            aria-invalid={isInvalid || undefined}
            aria-required={isRequired || undefined}
          />
        )}
      </div>
      {imageError !== null && (
        <Typography.Text type="danger" role="status">
          {imageError}
        </Typography.Text>
      )}
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
