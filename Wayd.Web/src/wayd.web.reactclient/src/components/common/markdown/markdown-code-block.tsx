'use client'

import React, { useRef } from 'react'
import { GlobalToken } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { CopyOutlined } from '@ant-design/icons'

export interface MarkdownCodeBlockProps extends React.DetailedHTMLProps<
  React.HTMLAttributes<HTMLPreElement>,
  HTMLPreElement
> {
  node?: any
  token: GlobalToken
}

type CodeElementProps = {
  className?: string
  children?: React.ReactNode
  node?: { tagName?: string }
}

/**
 * Markdown puts a fence's text in `<pre><code class="language-x">`. The inner element arrives already
 * rendered through the `code` mapping, which styles inline code, so its content and language class
 * are lifted out and placed in this block's own `<code>`. Anything else — raw-HTML `<pre>` holding a
 * link or several nodes — is kept as it is.
 */
const unwrapCode = (children: React.ReactNode): CodeElementProps => {
  const items = React.Children.toArray(children)
  const only = items.length === 1 ? items[0] : undefined
  if (
    React.isValidElement<CodeElementProps>(only) &&
    (only.type === 'code' || only.props.node?.tagName === 'code')
  ) {
    const { className, children: content } = only.props
    return { className, children: content }
  }
  return { children }
}

/** A fenced code block with a copy-to-clipboard button. */
const MarkdownCodeBlock: React.FC<MarkdownCodeBlockProps> = ({
  token,
  children,
  style,
  ...rest
}) => {
  const preRef = useRef<HTMLPreElement>(null)

  if (!children) {
    return null
  }

  const code = unwrapCode(children)

  const copyToClipboard = () => {
    const codeText = preRef.current?.textContent ?? ''
    if (codeText) {
      navigator.clipboard
        .writeText(codeText)
        .catch((err) => console.error('Failed to copy to clipboard', err))
    }
  }

  return (
    <div
      style={{
        display: 'block',
        padding: token.padding,
        background: token.colorFillTertiary,
        borderRadius: token.borderRadius,
        overflowX: 'auto',
        position: 'relative',
      }}
    >
      <WaydTooltip title="Copy">
        <CopyOutlined
          role="button"
          aria-label="Copy code to clipboard"
          onClick={copyToClipboard}
          style={{
            position: 'absolute',
            top: 8,
            right: 8,
            cursor: 'pointer',
          }}
        />
      </WaydTooltip>
      <pre {...rest} ref={preRef}>
        <code
          className={code.className}
          style={{ ...style, fontFamily: 'monospace', fontSize: '0.9em' }}
        >
          {code.children}
        </code>
      </pre>
    </div>
  )
}

export default MarkdownCodeBlock
