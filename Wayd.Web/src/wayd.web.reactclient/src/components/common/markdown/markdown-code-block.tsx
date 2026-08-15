import React, { useEffect, useRef } from 'react'
import { GlobalToken } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { CopyOutlined } from '@ant-design/icons'

export interface MarkdownCodeBlockProps
  extends React.DetailedHTMLProps<
    React.HTMLAttributes<HTMLPreElement>,
    HTMLPreElement
  > {
  node?: any
  token: GlobalToken
}

const MarkdownCodeBlock: React.FC<MarkdownCodeBlockProps> = ({
  token,
  children,
  style,
  ...rest
}) => {
  const codeRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (codeRef.current) {
      const codeElement = codeRef.current.querySelector('code')
      if (codeElement) {
        const spans = codeElement.querySelectorAll('span')
        spans.forEach((span) => {
          span.removeAttribute('class')
        })
      }
    }
  }, [])

  // TODO: this is only getting the top level children, need to handle nested children
  // this is probably why the copyToClipboard test is failing
  if (!children) {
    return null
  }

  const copyToClipboard = () => {
    const codeText = codeRef.current?.innerText || ''
    if (codeText) {
      navigator.clipboard
        .writeText(codeText)
        .catch((err) => console.error('Failed to copy to clipboard', err))
    }
  }

  return (
    <div
      ref={codeRef}
      style={{
        display: 'block',
        padding: token.padding,
        background: token.colorFillTertiary, // TODO: get this closer to the actual code block color <Text code {...props} />,
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
      <pre {...rest}>
        <code style={{ ...style, fontFamily: 'monospace', fontSize: '0.9em' }}>
          {children}
        </code>
      </pre>
    </div>
  )
}

export default MarkdownCodeBlock
