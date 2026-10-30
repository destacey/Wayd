/**
 * The markdown to store for what the editor serialized. The editor keeps an empty paragraph after
 * a trailing list, table or rule so the user can type below it; that paragraph serializes as
 * trailing newlines, which carry no meaning and would count against the length limit.
 */
export const toStoredMarkdown = (markdown: string): string =>
  markdown.replace(/\n+$/, '')
