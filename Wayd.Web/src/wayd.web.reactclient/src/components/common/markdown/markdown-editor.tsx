'use client'

import dynamic from 'next/dynamic'
import { useFeatureFlag } from '@/src/hooks'
import LegacyMarkdownEditor from './legacy-markdown-editor'
import type { WysiwygMarkdownEditorProps } from './wysiwyg/wysiwyg-markdown-editor'

/** The flag that switches every long-text field to the document editor. */
export const WYSIWYG_MARKDOWN_EDITOR_FLAG = 'wysiwyg-markdown-editor'

const WysiwygMarkdownEditor = dynamic(
  () => import('./wysiwyg/wysiwyg-markdown-editor'),
  { ssr: false },
)

export type MarkdownEditorProps = WysiwygMarkdownEditorProps

/**
 * Editor for a long-text field stored as markdown. Shows the document editor when the
 * `wysiwyg-markdown-editor` flag is on and the markdown text editor otherwise; both take the same
 * props, so forms do not know which they get.
 */
const MarkdownEditor = (props: MarkdownEditorProps) => {
  const { isEnabled, isLoading } = useFeatureFlag(WYSIWYG_MARKDOWN_EDITOR_FLAG)

  // Waiting avoids mounting one editor and swapping it for the other a moment later.
  if (isLoading) return null
  return isEnabled ? (
    <WysiwygMarkdownEditor {...props} />
  ) : (
    <LegacyMarkdownEditor {...props} />
  )
}

export default MarkdownEditor
