import { GlobalToken, Typography } from 'antd'

const { Paragraph } = Typography

export interface MarkdownBlockquoteProps
  extends React.DetailedHTMLProps<
    React.BlockquoteHTMLAttributes<HTMLQuoteElement>,
    HTMLQuoteElement
  > {
  node?: any
  token: GlobalToken
}

// `token` is destructured out rather than spread, so the theme object is not
// forwarded onto the DOM element Paragraph renders.
const MarkdownBlockquote = ({
  token,
  children,
  style,
  ...rest
}: MarkdownBlockquoteProps) => {
  const blockquoteStyles = {
    paddingTop: '14px',
    paddingBottom: '2px',
    paddingLeft: token.padding,
    paddingRight: token.padding,
    borderLeft: `${token.lineWidthBold}px solid ${token.colorPrimary}`,
    background: token.colorFillTertiary, // TODO: get this closer to the actual code block color <Text code {...props} />,
  }

  if (!children || (typeof children === 'string' && children.trim() === '')) {
    return null
  }

  return (
    <Paragraph
      {...rest}
      style={{
        ...style,
        ...blockquoteStyles,
      }}
    >
      {children}
    </Paragraph>
  )
}

export default MarkdownBlockquote
