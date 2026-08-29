'use client'

import { MDXRemote, MDXRemoteSerializeResult } from 'next-mdx-remote'
import Link from 'next/link'
import { Typography } from 'antd'
import MermaidDiagram from './mermaid-diagram'

const { Title, Paragraph } = Typography

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

interface DocsPageContentProps {
  title: string
  description?: string
  mdxSource: MDXRemoteSerializeResult
}

export default function DocsPageContent({
  mdxSource,
}: DocsPageContentProps) {
  return (
    <div className="docs-content">
      <MDXRemote {...mdxSource} components={mdxComponents} />
    </div>
  )
}
