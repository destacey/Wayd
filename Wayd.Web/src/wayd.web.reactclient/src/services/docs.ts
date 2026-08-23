import fs from 'fs'
import path from 'path'
import matter from 'gray-matter'

// Path to the shared docs folder.
// DOCS_PATH env var allows overriding in Docker/CI (e.g., DOCS_PATH=/docs).
// Default: 3 levels up from cwd (Wayd.Web/src/wayd.web.reactclient) to repo root.
const DOCS_ROOT = process.env.DOCS_PATH
  ? path.resolve(process.env.DOCS_PATH)
  : path.join(process.cwd(), '..', '..', '..', 'docs')

export interface DocFrontmatter {
  title: string
  description?: string
  sidebar_position?: number
  audience?: string[]
}

export interface DocPage {
  slug: string[]
  frontmatter: DocFrontmatter
  content: string
}

export interface DocNavItem {
  title: string
  slug: string
  children?: DocNavItem[]
  position: number
}

// Slug segments arrive straight from the URL. Next.js percent-decodes them before the
// handler sees them, so `/docs/..%2fREADME` yields the single segment `../README` — the
// separator is already decoded by the time it reaches here, and no amount of URL-level
// normalisation upstream has removed it.
const REJECTED_SEGMENT = /^$|^\.\.$|[/\\]/

/**
 * Reject slug segments that could steer resolution outside the docs tree before any
 * filesystem access happens. Empty, `..`, or separator-bearing segments are never
 * legitimate: real slugs come from file and directory names.
 */
function hasUnsafeSegment(slug: string[]): boolean {
  return slug.some((segment) => REJECTED_SEGMENT.test(segment))
}

/**
 * The single chokepoint for turning a candidate path into a usable one.
 *
 * The containment check is NOT redundant with the segment rejection above, and it is not
 * redundant with `path.join`: `path.join` *collapses* `..` rather than rejecting it, so it
 * resolves happily to a location outside DOCS_ROOT. Every candidate must pass through here.
 *
 * The trailing `path.sep` matters — without it a sibling directory such as `docs-site`
 * would satisfy a naive `startsWith(DOCS_ROOT)`.
 */
function containedPath(candidate: string): string | null {
  const resolved = path.resolve(candidate)
  const root = path.resolve(DOCS_ROOT)

  if (resolved !== root && !resolved.startsWith(root + path.sep)) return null

  // isFile(), not existsSync(): a directory named `foo.md` or `index.md` would otherwise be
  // returned here and blow up in getDocBySlug's readFileSync with EISDIR.
  return fs.statSync(resolved, { throwIfNoEntry: false })?.isFile()
    ? resolved
    : null
}

/**
 * Get the absolute path to a doc file from its slug segments.
 * Tries slug as a file first, then as a directory with index.
 *
 * Returns null for anything that does not resolve to a real file inside DOCS_ROOT.
 */
export function resolveDocPath(slug: string[]): string | null {
  if (hasUnsafeSegment(slug)) return null

  const relativePath = slug.join('/')

  // Ordered by preference: exact file match first, then the directory-index forms.
  // Add new candidates to this list only — containedPath() is the sole gate, so a
  // candidate added anywhere else would bypass the containment check.
  const candidates = [
    path.join(DOCS_ROOT, `${relativePath}.mdx`),
    path.join(DOCS_ROOT, `${relativePath}.md`),
    path.join(DOCS_ROOT, relativePath, 'index.mdx'),
    path.join(DOCS_ROOT, relativePath, 'index.md'),
  ]

  for (const candidate of candidates) {
    const safe = containedPath(candidate)
    if (safe) return safe
  }

  return null
}

/**
 * Load a single doc page by its slug segments.
 */
export function getDocBySlug(slug: string[]): DocPage | null {
  const filePath = resolveDocPath(slug)
  if (!filePath) return null

  const fileContents = fs.readFileSync(filePath, 'utf8')
  const { data, content } = matter(fileContents)

  return {
    slug,
    frontmatter: data as DocFrontmatter,
    content,
  }
}

/**
 * Get all doc slugs for static generation.
 */
export function getAllDocSlugs(): string[][] {
  const slugs: string[][] = []

  function walkDir(dir: string, parentSlug: string[] = []) {
    if (!fs.existsSync(dir)) return

    const entries = fs.readdirSync(dir, { withFileTypes: true })

    for (const entry of entries) {
      // Skip hidden files, _legacy folders, and node_modules.
      // Note: ai/ folder IS included here (pages are generated) but is
      // excluded from getDocsNavigation() so it doesn't appear in the sidebar.
      if (
        entry.name.startsWith('.') ||
        entry.name.startsWith('_') ||
        entry.name === 'node_modules'
      ) {
        continue
      }

      if (entry.isDirectory()) {
        const dirSlug = [...parentSlug, entry.name]
        // If the directory has an index file, add it
        const indexPath = path.join(dir, entry.name, 'index.mdx')
        const indexMdPath = path.join(dir, entry.name, 'index.md')
        if (fs.existsSync(indexPath) || fs.existsSync(indexMdPath)) {
          slugs.push(dirSlug)
        }
        walkDir(path.join(dir, entry.name), dirSlug)
      } else if (
        (entry.name.endsWith('.mdx') || entry.name.endsWith('.md')) &&
        entry.name !== 'index.mdx' &&
        entry.name !== 'index.md'
      ) {
        const name = entry.name.replace(/\.mdx?$/, '')
        slugs.push([...parentSlug, name])
      }
    }
  }

  // Add root index
  slugs.push([])
  walkDir(DOCS_ROOT)

  return slugs
}

/**
 * Build the navigation tree for the docs sidebar.
 */
export function getDocsNavigation(): DocNavItem[] {
  function buildNav(dir: string, basePath: string): DocNavItem[] {
    if (!fs.existsSync(dir)) return []

    const entries = fs.readdirSync(dir, { withFileTypes: true })
    const items: DocNavItem[] = []

    for (const entry of entries) {
      if (
        entry.name.startsWith('.') ||
        entry.name.startsWith('_') ||
        entry.name === 'node_modules' ||
        entry.name === 'ai'
      ) {
        continue
      }

      if (entry.isDirectory()) {
        const indexPath = path.join(dir, entry.name, 'index.mdx')
        const indexMdPath = path.join(dir, entry.name, 'index.md')
        const indexFile = fs.existsSync(indexPath)
          ? indexPath
          : fs.existsSync(indexMdPath)
            ? indexMdPath
            : null

        if (indexFile) {
          const { data } = matter(fs.readFileSync(indexFile, 'utf8'))
          const slug = basePath
            ? `${basePath}/${entry.name}`
            : entry.name
          const children = buildNav(
            path.join(dir, entry.name),
            slug,
          )

          items.push({
            title: (data as DocFrontmatter).title || entry.name,
            slug,
            children: children.length > 0 ? children : undefined,
            position: (data as DocFrontmatter).sidebar_position ?? 99,
          })
        }
      } else if (
        (entry.name.endsWith('.mdx') || entry.name.endsWith('.md')) &&
        entry.name !== 'index.mdx' &&
        entry.name !== 'index.md'
      ) {
        const name = entry.name.replace(/\.mdx?$/, '')
        const filePath = path.join(dir, entry.name)
        const { data } = matter(fs.readFileSync(filePath, 'utf8'))
        const slug = basePath ? `${basePath}/${name}` : name

        items.push({
          title: (data as DocFrontmatter).title || name,
          slug,
          position: (data as DocFrontmatter).sidebar_position ?? 99,
        })
      }
    }

    return items.sort((a, b) => a.position - b.position)
  }

  return buildNav(DOCS_ROOT, '')
}
