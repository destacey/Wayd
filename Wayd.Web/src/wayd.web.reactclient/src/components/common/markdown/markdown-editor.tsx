'use client'

import dynamic from 'next/dynamic'
import type { WysiwygMarkdownEditorProps } from './wysiwyg/wysiwyg-markdown-editor'

/** Roughly the height the editor takes with its footer, in pixels. */
const EDITOR_HEIGHT = 220

// Holds the space while the editor chunk loads, so a form dialog does not jump when it arrives.
const WysiwygMarkdownEditor = dynamic(
  () => import('./wysiwyg/wysiwyg-markdown-editor'),
  {
    ssr: false,
    loading: () => (
      <div aria-busy="true" style={{ minHeight: EDITOR_HEIGHT }} />
    ),
  },
)

export type MarkdownEditorProps = WysiwygMarkdownEditorProps

/**
 * Editor for a long-text field stored as markdown: a document editor that formats as the user
 * types, loaded on first use.
 */
const MarkdownEditor = (props: MarkdownEditorProps) => (
  <WysiwygMarkdownEditor {...props} />
)

export default MarkdownEditor
