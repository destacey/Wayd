import { Editor, type JSONContent } from '@tiptap/core'
import { markdownEditorExtensions } from '../wysiwyg/markdown-extensions'

/** The outcome of loading markdown into the editor and serializing it straight back. */
export interface RoundTripResult {
  /** The markdown the editor produced without any edit. */
  output: string
  /** The document the original parsed to. */
  document: JSONContent
  /** The document the output parses to; equal to `document` when the round trip is stable. */
  reparsedDocument: JSONContent
  /** The result of a second round trip; equal to `output` when the serializer is idempotent. */
  secondOutput: string
}

/**
 * Creates a headless editor (no DOM element is mounted) with the extensions `MarkdownEditor`
 * ships. Reuse one across many round trips: building the schema is the expensive part.
 */
export const createHeadlessMarkdownEditor = (): Editor =>
  new Editor({ element: null, extensions: markdownEditorExtensions() })

/** Loads `markdown` into `editor` as markdown and serializes it back, twice. */
export const roundTrip = (
  editor: Editor,
  markdown: string,
): RoundTripResult => {
  editor.commands.setContent(markdown, { contentType: 'markdown' })
  const document = editor.getJSON()
  const output = editor.getMarkdown()

  editor.commands.setContent(output, { contentType: 'markdown' })
  const reparsedDocument = editor.getJSON()
  const secondOutput = editor.getMarkdown()

  return { output, document, reparsedDocument, secondOutput }
}
