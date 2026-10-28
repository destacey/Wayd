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

/** Matches links that leave the app (`https://…`, `//host/…`); anchors, relative paths and `mailto:` stay in the tab. */
const EXTERNAL_HREF = /^(https?:)?\/\//i

interface MarkdownLinkProps extends Omit<
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

  // Every mapping below strips react-markdown's `node` (a hast object) before
  // spreading, so it is never forwarded onto a DOM element — antd's Typography
  // and the local markdown components pass unknown props straight through.
  return {
    h1: ({ node: _node, ...props }) => <Title level={1} {...props} />,
    h2: ({ node: _node, ...props }) => <Title level={2} {...props} />,
    h3: ({ node: _node, ...props }) => <Title level={3} {...props} />,
    h4: ({ node: _node, ...props }) => <Title level={4} {...props} />,
    h5: ({ node: _node, ...props }) => <Title level={5} {...props} />,
    p: ({ node: _node, ...props }) => <Paragraph {...props} />,
    strong: ({ node: _node, ...props }) => <Text strong {...props} />,
    em: ({ node: _node, ...props }) => <Text italic {...props} />,
    u: ({ node: _node, ...props }) => <Text underline {...props} />, // TODO: add to toolbar
    del: ({ node: _node, ...props }) => <Text delete {...props} />,
    code: ({ node: _node, ...props }) => <Text code {...props} />,
    pre: ({ node: _node, ...props }: MarkdownCodeBlockProps) => (
      <MarkdownCodeBlock {...props} token={token} />
    ),
    blockquote: ({ node: _node, ...props }: MarkdownBlockquoteProps) => (
      <MarkdownBlockquote {...props} token={token} />
    ),
    a: ({ node: _node, children, href, ...props }: MarkdownLinkProps) =>
      href && EXTERNAL_HREF.test(href) ? (
        <AntDLink
          href={href}
          target="_blank"
          rel="noopener noreferrer"
          {...props}
        >
          {children}
        </AntDLink>
      ) : (
        <AntDLink href={href} {...props}>
          {children}
        </AntDLink>
      ),
    // `ref` is dropped as well because antd 6.6 types Divider's ref as DividerRef,
    // which is incompatible with the HTMLHRElement ref react-markdown passes for <hr>.
    hr: ({ ref: _ref, node: _node, ...props }) => <Divider {...props} />,
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
    table: ({ node: _node, ...props }) => <MarkdownTable {...props} />,
  } as Components
}

/**
 * The same components typed for `@uiw/react-md-editor`'s preview, whose `components` prop is
 * declared against an older react-markdown signature.
 */
export const useMarkdownComponentsForMDEditor = (): MDEditorComponents =>
  useMarkdownComponents() as MDEditorComponents
