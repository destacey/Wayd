'use client'

import { Divider, Image, Typography } from 'antd'
import useTheme from '../../contexts/theme'
import React from 'react'
import { Components } from 'react-markdown'
import { LinkProps } from 'antd/es/typography/Link'
import {
  MarkdownBlockquote,
  MarkdownBlockquoteProps,
  MarkdownCodeBlock,
  MarkdownCodeBlockProps,
  MarkdownTable,
} from '.'

// Type for MDEditor's expected Components (compatible with older react-markdown)
type MDEditorComponents = {
  [key: string]: React.ComponentType<any>
}

const { Title, Paragraph, Text, Link: AntDLink } = Typography

interface MarkdownLinkProps
  extends Omit<
    React.DetailedHTMLProps<
      React.AnchorHTMLAttributes<HTMLAnchorElement>,
      HTMLAnchorElement
    >,
    'type'
  > {
  node?: any
  type?: LinkProps['type']
}

export const useMarkdownComponents = (): Components => {
  const { token } = useTheme()

  return ({
    h1: (props) => <Title level={1} {...props} />,
    h2: (props) => <Title level={2} {...props} />,
    h3: (props) => <Title level={3} {...props} />,
    h4: (props) => <Title level={4} {...props} />,
    h5: (props) => <Title level={5} {...props} />,
    p: (props) => <Paragraph {...props} />,
    strong: (props) => <Text strong {...props} />,
    em: (props) => <Text italic {...props} />,
    u: (props) => <Text underline {...props} />, // TODO: add to toolbar
    del: (props) => <Text delete {...props} />,
    code: (props) => <Text code {...props} />,
    pre: ({ ...props }: MarkdownCodeBlockProps) => (
      <MarkdownCodeBlock {...props} token={token} />
    ), // TODO: needs styling and syntax improvements
    blockquote: (props: MarkdownBlockquoteProps) => (
      <MarkdownBlockquote {...props} token={token} />
    ),
    a: ({ node, children, ...props }: MarkdownLinkProps) => (
      <AntDLink target="_blank" rel="noopener noreferrer" {...props}>
        {children}
      </AntDLink>
    ),
    // antd 6.6 types Divider's ref as DividerRef, not the HTMLHRElement ref
    // react-markdown hands down for <hr>; it is unused here, so drop it.
    hr: ({ ref: _ref, ...props }) => <Divider {...props} />,
    img: ({ node: _node, src: rawSrc, alt, ...rest }) => {
      const src = typeof rawSrc === 'string' ? rawSrc : undefined
      return (
        <Image
          src={src}
          alt={alt || 'Image'}
          fallback="/images/fallback-image.png"
          {...(rest as object)}
        />
      )
    }, // TODO: needs improvement, especially for background
    table: (props) => <MarkdownTable {...props} />,
  }) as Components
}

// Type-safe adapter for MDEditor compatibility
export const useMarkdownComponentsForMDEditor = (): MDEditorComponents => {
  const components = useMarkdownComponents()

  // Convert to the format expected by MDEditor's older react-markdown version
  const adapted: MDEditorComponents = {}

  // Map each component with proper type compatibility
  Object.entries(components).forEach(([key, Component]) => {
    adapted[key] = Component as React.ComponentType<any>
  })

  return adapted
}
