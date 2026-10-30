/**
 * Markdown the renderer displays but the editor cannot keep: editing a record that holds any of it
 * removes it. Each entry names the construct in words a non-developer recognises.
 *
 * Code is stripped before matching, so a code sample that mentions `<details>` is not reported.
 */
const unsupportedConstructs: { label: string; pattern: RegExp }[] = [
  { label: 'footnotes', pattern: /\[\^[^\]\s]+\]/ },
  { label: 'collapsible sections', pattern: /<details[\s>]/i },
  { label: 'keyboard keys', pattern: /<kbd[\s>]/i },
  { label: 'HTML comments', pattern: /<!--/ },
  { label: 'HTML blocks', pattern: /<(div|span|p|section|center|font)[\s>]/i },
  {
    label: 'special characters written as HTML entities',
    pattern: /&(?!(?:lt|gt|amp|quot);)[a-z][a-z0-9]*;/i,
  },
]

/** Fenced blocks, then inline code spans. */
const CODE =
  /(^|\n)(`{3,}|~{3,})[^\n]*\n[\s\S]*?\n\2[^\S\n]*(?=\n|$)|(`+)[\s\S]*?\3/g

/** Names each kind of content in `markdown` that editing would remove, in a fixed order. */
export const findUnsupportedMarkdown = (
  markdown: string | undefined,
): string[] => {
  if (!markdown) return []
  const prose = markdown.replace(CODE, '')
  return unsupportedConstructs
    .filter(({ pattern }) => pattern.test(prose))
    .map(({ label }) => label)
}
