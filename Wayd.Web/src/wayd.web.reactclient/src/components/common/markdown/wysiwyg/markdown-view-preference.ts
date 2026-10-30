/** How the editor shows a field: as a formatted document, or as its markdown source. */
export type MarkdownView = 'document' | 'source'

/** The browser storage entry that remembers the view a person last chose, for every field. */
export const MARKDOWN_VIEW_KEY = 'wayd-markdown-editor:view'

/**
 * The view to open an editor in. Anything but a stored `source` opens the document view, including
 * storage that is blocked or throws, as it can in a private window.
 */
export const readMarkdownView = (): MarkdownView => {
  try {
    return window.localStorage.getItem(MARKDOWN_VIEW_KEY) === 'source'
      ? 'source'
      : 'document'
  } catch {
    return 'document'
  }
}

/** Remembers `view` for the next editor this browser opens; a storage failure only forgets it. */
export const saveMarkdownView = (view: MarkdownView): void => {
  try {
    window.localStorage.setItem(MARKDOWN_VIEW_KEY, view)
  } catch {
    // Remembering the view is a convenience; the editor works without it.
  }
}
