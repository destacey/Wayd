import { compileMDX } from 'next-mdx-remote/rsc'
import Link from 'next/link'
import type { ReactElement } from 'react'
import remarkGfm from 'remark-gfm'
import remarkDirective from 'remark-directive'
import remarkDocsAdmonitions from '@/src/lib/remark-docs-admonitions'
import remarkDocsLinks from '@/src/lib/remark-docs-links'
import MermaidDiagram from './mermaid-diagram'

function DocsLink({
  href,
  children,
  ...props
}: React.AnchorHTMLAttributes<HTMLAnchorElement>) {
  if (!href) return <a {...props}>{children}</a>

  // External links — open in new tab
  if (href.startsWith('http://') || href.startsWith('https://')) {
    return (
      <a href={href} target="_blank" rel="noopener noreferrer" {...props}>
        {children}
      </a>
    )
  }

  // Internal links (absolute paths from remark plugin, or anchors)
  return (
    <Link href={href} {...props}>
      {children}
    </Link>
  )
}

// Custom components for MDX rendering
const mdxComponents = {
  a: DocsLink,
  pre: ({ children, ...props }: React.HTMLAttributes<HTMLPreElement>) => {
    const child = children as React.ReactElement<{
      className?: string
      children?: string
    }>
    if (child?.props?.className === 'language-mermaid') {
      return <MermaidDiagram chart={String(child.props.children).trim()} />
    }
    return <pre {...props}>{children}</pre>
  },
}

// Docs ship with the build and never change while it runs, so each page is compiled once per process.
// Development compiles on every request so an edited page shows on reload.
const compiled = new Map<string, Promise<ReactElement>>()

async function compile(content: string, fileSlug: string[]) {
  const { content: element } = await compileMDX({
    source: content,
    components: mdxComponents,
    options: {
      mdxOptions: {
        remarkPlugins: [
          remarkGfm,
          remarkDirective,
          remarkDocsAdmonitions,
          [remarkDocsLinks, { slug: fileSlug, basePath: '/docs' }],
        ],
      },
    },
  })
  return element
}

function compileOnce(content: string, fileSlug: string[]) {
  if (process.env.NODE_ENV === 'development') return compile(content, fileSlug)

  const key = fileSlug.join('/')
  let element = compiled.get(key)
  if (!element) {
    element = compile(content, fileSlug)
    // A failed compile is not kept, so the next request tries again.
    element.catch(() => compiled.delete(key))
    compiled.set(key, element)
  }
  return element
}

interface DocsPageContentProps {
  /** The page's MDX source. */
  content: string
  /**
   * The file's path under the docs root, ending in `index` for a directory's page; relative links
   * resolve against it.
   */
  fileSlug: string[]
}

/**
 * Renders a docs page's MDX on the server. The compiled page runs here, never in the browser, so the
 * client's Content-Security-Policy needs no `'unsafe-eval'`.
 */
export default async function DocsPageContent({
  content,
  fileSlug,
}: DocsPageContentProps) {
  return (
    <div className="docs-content">{await compileOnce(content, fileSlug)}</div>
  )
}
