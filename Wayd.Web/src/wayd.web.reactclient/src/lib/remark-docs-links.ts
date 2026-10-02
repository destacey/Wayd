/**
 * Remark plugin that transforms relative links in MDX docs to absolute /docs/ paths.
 *
 * MDX files use relative paths (e.g., `../planning/risks`) based on
 * their file system location. These work in Docusaurus but not in the Next.js app
 * because the browser resolves them against the URL, not the file path.
 *
 * This plugin runs at serialize time and rewrites links to absolute paths based on
 * the source file's slug, so `../planning/risks` in a file at
 * `user-guide/work-management/work-items` becomes `/docs/user-guide/planning/risks`.
 *
 * Options:
 *   - slug: string[] — The slug segments of the current doc
 *       For index pages: ['user-guide', 'planning', 'index']
 *       For leaf pages:  ['user-guide', 'planning', 'risks']
 *   - basePath: string — The base URL path for docs (default: '/docs')
 */
import { visit } from 'unist-util-visit'
import path from 'path'

interface RemarkDocsLinksOptions {
  slug?: string[]
  basePath?: string
}

export default function remarkDocsLinks(options: RemarkDocsLinksOptions = {}) {
  const { slug = [], basePath = '/docs' } = options

  // Determine the source file's directory in the docs tree.
  // For index pages (slug ends with 'index'), the directory is the slug without 'index'.
  //   e.g., ['user-guide', 'planning', 'index'] -> 'user-guide/planning'
  // For leaf pages, the directory is the slug without the last segment (the filename).
  //   e.g., ['user-guide', 'planning', 'sprints'] -> 'user-guide/planning'
  // Both cases: strip the last segment to get the directory.
  const sourceDir = slug.length > 1 ? slug.slice(0, -1).join('/') : ''

  return (tree: any) => {
    visit(tree, 'link', (node: any) => {
      const href = node.url
      if (!href) return

      // Skip external links, anchors, and already-absolute paths
      if (
        href.startsWith('http://') ||
        href.startsWith('https://') ||
        href.startsWith('#') ||
        href.startsWith('/')
      ) {
        return
      }

      // Split href into path and hash
      const hashIndex = href.indexOf('#')
      const hrefPath = hashIndex >= 0 ? href.substring(0, hashIndex) : href
      const hash = hashIndex >= 0 ? href.substring(hashIndex) : ''

      if (!hrefPath.startsWith('./') && !hrefPath.startsWith('../')) {
        return
      }

      // Resolve the relative path against the source directory
      const resolved = path.posix.normalize(
        path.posix.join(sourceDir, hrefPath),
      )

      // Strip file extensions (.mdx, .md) and trailing /index
      const cleaned = resolved.replace(/\.mdx?$/, '').replace(/\/index$/, '')

      // Build the absolute URL
      node.url = `${basePath}/${cleaned}${hash}`
    })
  }
}
