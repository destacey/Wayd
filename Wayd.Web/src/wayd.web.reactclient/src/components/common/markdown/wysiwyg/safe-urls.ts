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
