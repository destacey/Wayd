import { GlobalToken } from 'antd'

export interface MarkdownBlockquoteProps extends React.DetailedHTMLProps<
  React.BlockquoteHTMLAttributes<HTMLQuoteElement>,
  HTMLQuoteElement
> {
  node?: any
  token: GlobalToken
}

// `token` is destructured out rather than spread, so the theme object is not
// forwarded onto the DOM element.
const MarkdownBlockquote = ({
  token,
  children,
  style,
  ...rest
}: MarkdownBlockquoteProps) => {
  const blockquoteStyles = {
    margin: `0 0 ${token.marginSM}px`,
    paddingTop: '14px',
    paddingBottom: '2px',
    paddingLeft: token.padding,
    paddingRight: token.padding,
    borderLeft: `${token.lineWidthBold}px solid ${token.colorPrimary}`,
    background: token.colorFillTertiary,
  }

  if (!children || (typeof children === 'string' && children.trim() === '')) {
    return null
  }

  return (
    <blockquote
      {...rest}
      style={{
        ...style,
        ...blockquoteStyles,
      }}
    >
      {children}
    </blockquote>
  )
}

export default MarkdownBlockquote
