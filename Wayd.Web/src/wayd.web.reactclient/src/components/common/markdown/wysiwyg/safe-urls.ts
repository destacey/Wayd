import { defaultSchema } from 'rehype-sanitize'

/** Schemes `MarkdownRenderer` keeps on a link; any other scheme is blanked when it renders. */
const linkSchemes = defaultSchema.protocols?.href ?? ['http', 'https', 'mailto']

const SCHEME = /^([a-z][a-z0-9+.-]*):/i

/**
 * Whether `href` may be stored on a link: a scheme the sanitizer keeps, or a relative URL (`/path`,
 * `#anchor`, `page`). Whitespace and control characters are stripped first, as browsers do, so
 * `java\nscript:` cannot slip through. An `&` or `%` before the first `/`, `?` or `#` is refused,
 * since an encoded colon (`javascript&colon;`) would hide a scheme from this check.
 */
export const isSafeLinkHref = (href: string | null | undefined): boolean => {
  if (!href) return false
  const compact = href.replace(/[\s\u0000-\u001f]/g, '')
  const beforePath = compact.split(/[/?#]/, 1)[0]
  if (/[&%]/.test(beforePath)) return false
  const scheme = SCHEME.exec(compact)?.[1]
  return !scheme || linkSchemes.includes(scheme.toLowerCase())
}

/**
 * A bare host (`wayd.dev/x`), which a browser would otherwise read as a relative path. The last
 * label must be letters, so `v1.2` is not a host.
 */
const BARE_HOST = /^[a-z0-9-]+(?:\.[a-z0-9-]+)*\.([a-z]{2,})(?=[/:?#]|$)/i

/** Endings that make `page.html` or `report.pdf` a file name rather than a host. */
const FILE_EXTENSIONS = new Set([
  'htm',
  'html',
  'php',
  'asp',
  'aspx',
  'jsp',
  'md',
  'txt',
  'csv',
  'json',
  'xml',
  'pdf',
  'doc',
  'docx',
  'xls',
  'xlsx',
  'ppt',
  'pptx',
  'png',
  'jpg',
  'jpeg',
  'gif',
  'svg',
  'zip',
])

/**
 * The href to store for what the user typed, or `null` when it is not allowed. A bare host gets
 * `https://`; anything else is kept as typed, so `page.html` stays a relative link.
 */
export const toLinkHref = (input: string): string | null => {
  const trimmed = input.trim()
  if (!trimmed) return null
  const topLevel = BARE_HOST.exec(trimmed)?.[1]?.toLowerCase()
  const isHost = !!topLevel && !FILE_EXTENSIONS.has(topLevel)
  const href = isHost ? `https://${trimmed}` : trimmed
  return isSafeLinkHref(href) ? href : null
}

/** Whether `src` may be stored on an image: `http` or `https` only, as the sanitizer allows. */
export const isSafeImageSrc = (src: string | null | undefined): boolean =>
  !!src && /^https?:\/\//i.test(src.trim())

const DESTINATION_ESCAPES: Record<string, string> = {
  '(': '%28',
  ')': '%29',
  '<': '%3C',
  '>': '%3E',
}

/**
 * `url` made safe to write as a markdown link or image destination: whitespace, parentheses and
 * angle brackets are percent-encoded, since any of them would end `[text](url)` early or break it.
 */
export const toMarkdownDestination = (url: string): string =>
  url.replace(
    /[\s()<>]/g,
    (char) => DESTINATION_ESCAPES[char] ?? encodeURIComponent(char),
  )
