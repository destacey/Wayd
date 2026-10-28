'use client'

import React from 'react'
import ReactMarkdown, { type Options } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { useMarkdownComponents } from '.'
import rehypeRaw from 'rehype-raw'
import rehypeSanitize from 'rehype-sanitize'
import { sanitizeSchema } from './sanitize-schema'
import './markdown-renderer.css'

const remarkPlugins: Options['remarkPlugins'] = [remarkGfm]

// rehypeSanitize must stay after rehypeRaw — sanitizing first would leave the raw HTML unparsed and
// let it through untouched.
const rehypePlugins: Options['rehypePlugins'] = [
  rehypeRaw,
  [rehypeSanitize, sanitizeSchema],
]

interface MarkdownRendererProps {
  markdown?: string
}

const MarkdownRenderer: React.FC<MarkdownRendererProps> = ({ markdown }) => {
  const markdownComponents = useMarkdownComponents()

  if (!markdown) return null
  return (
    <div className="wayd-markdown-renderer">
      <ReactMarkdown
        components={markdownComponents}
        remarkPlugins={remarkPlugins}
        rehypePlugins={rehypePlugins}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  )
}

export default MarkdownRenderer
