import { Editor, type Extensions, type JSONContent } from '@tiptap/core'
import { TableKit } from '@tiptap/extension-table'
import { TaskItem, TaskList } from '@tiptap/extension-list'
import { Markdown } from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'

/**
 * The extensions the WYSIWYG `MarkdownEditor` would ship with (#1201): StarterKit (which includes
 * Link, Underline and CodeBlock), tables, and nestable task lists, plus the official markdown
 * parser and serializer. The round-trip harness measures exactly this set, so a change here is a
 * change to what the harness proves.
 */
export const markdownEditorExtensions = (): Extensions => [
  StarterKit,
  TableKit,
  TaskList,
  TaskItem.configure({ nested: true }),
  Markdown,
]

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
 * Creates a headless editor (no DOM element is mounted) with the editor's extensions. Reuse one
 * across many round trips: building the schema is the expensive part.
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
