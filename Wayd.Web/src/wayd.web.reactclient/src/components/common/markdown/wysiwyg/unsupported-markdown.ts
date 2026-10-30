import { sanitizeSchema } from '../sanitize-schema'

/**
 * Raw HTML tags the editor turns into a node or mark of its own, so their formatting survives an
 * edit (written back as markdown, or as raw HTML for `u`, `sub` and `sup`).
 */
const KEPT_TAGS = new Set([
  'a',
  'b',
  'strong',
  'i',
  'em',
  's',
  'strike',
  'del',
  'code',
  'u',
  'sub',
  'sup',
  'br',
  'img',
  'p',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'ul',
  'ol',
  'li',
  'blockquote',
  'pre',
  'hr',
  'table',
  'thead',
  'tbody',
  'tr',
  'th',
  'td',
])

/**
 * Tags the renderer displays but the editor drops. Tags the sanitizer removes are left out: they
 * never displayed, so nothing a reader saw is lost.
 */
const DROPPED_TAGS = (sanitizeSchema.tagNames ?? []).filter(
  (tag) => !KEPT_TAGS.has(tag),
)

const DROPPED_TAG = new RegExp(`<(${DROPPED_TAGS.join('|')})(?=[\\s/>])`, 'gi')

/**
 * Markdown the renderer displays but the editor cannot keep, each with words a non-developer
 * recognises. A footnote counts only with its definition: without one, `[^1]` is literal text.
 */
const unsupportedConstructs: {
  find: (prose: string) => string | null
}[] = [
  {
    find: (prose) =>
      /^ {0,3}\[\^[^\]\s]+\]:/m.test(prose) ? 'footnotes' : null,
  },
  {
    find: (prose) => (/<!--/.test(prose) ? 'HTML comments' : null),
  },
  {
    find: (prose) => {
      const tags = [
        ...new Set(
          [...prose.matchAll(DROPPED_TAG)].map((m) => m[1].toLowerCase()),
        ),
      ]
      return tags.length > 0
        ? `HTML the editor does not support (${tags.map((tag) => `<${tag}>`).join(', ')})`
        : null
    },
  },
  {
    find: (prose) =>
      /<img\b(?![^>]*\bsrc\s*=\s*["']?https?:)/i.test(prose)
        ? 'images that are not web addresses'
        : null,
  },
  {
    find: (prose) =>
      /&(?!(?:lt|gt|amp|quot);)[a-z][a-z0-9]*;/i.test(prose)
        ? 'special characters written as HTML entities'
        : null,
  },
]

/**
 * Fenced blocks, then inline code spans. A span opens and closes with backtick runs of the same
 * length and cannot cross a blank line, so a stray backtick does not hide the text after it.
 */
const CODE =
  /(^|\n)(`{3,}|~{3,})[^\n]*\n[\s\S]*?\n\2[^\S\n]*(?=\n|$)|(?<!`)(`+)(?!`)(?:(?!\n[ \t]*\n)[\s\S])*?(?<!`)\3(?!`)/g

/** Names each kind of content in `markdown` that editing would remove, in a fixed order. */
export const findUnsupportedMarkdown = (
  markdown: string | undefined,
): string[] => {
  if (!markdown) return []
  const prose = markdown.replace(CODE, '')
  return unsupportedConstructs
    .map(({ find }) => find(prose))
    .filter((label): label is string => label !== null)
}
