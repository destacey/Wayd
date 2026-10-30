import { defaultSchema } from 'rehype-sanitize'

/** Schemes `MarkdownRenderer` keeps on a link; any other scheme is blanked when it renders. */
const linkSchemes = defaultSchema.protocols?.href ?? ['http', 'https', 'mailto']

const SCHEME = /^([a-z][a-z0-9+.-]*):/i

/**
 * Whether `href` may be stored on a link: a scheme the sanitizer keeps, or a relative URL (`/path`,
 * `#anchor`, `page`). Whitespace and control characters are stripped first, as browsers do, so
 * `java\nscript:` cannot slip through.
 */
export const isSafeLinkHref = (href: string | null | undefined): boolean => {
  if (!href) return false
  const compact = href.replace(/[\s\u0000-\u001f]/g, '')
  const scheme = SCHEME.exec(compact)?.[1]
  return !scheme || linkSchemes.includes(scheme.toLowerCase())
}

/** Whether `src` may be stored on an image: `http` or `https` only, as the sanitizer allows. */
export const isSafeImageSrc = (src: string | null | undefined): boolean =>
  !!src && /^https?:\/\//i.test(src.trim())
