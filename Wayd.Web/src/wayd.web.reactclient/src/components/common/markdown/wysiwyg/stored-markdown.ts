/**
 * The markdown to store for what the editor serialized, without leading or trailing blank lines.
 * The editor keeps an empty paragraph after a trailing list, table or rule so the user can type
 * below it, and writes a table with a blank line before it even at the start; neither carries
 * meaning, and both would count against the length limit.
 */
export const toStoredMarkdown = (markdown: string): string =>
  markdown.replace(/^\n+|\n+$/g, '')
