import React from 'react'
import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { MarkdownBlockquote, MarkdownBlockquoteProps } from '.'

describe('MarkdownBlockquote', () => {
  const mockThemeToken = {
    padding: '16px',
    lineWidthBold: 2,
    colorPrimary: '#1890ff',
    colorFillTertiary: '#ffffff',
  } as any

  const defaultProps: MarkdownBlockquoteProps = {
    token: mockThemeToken,
    children: 'This is a blockquote',
  }

  it('renders the blockquote with the correct styles and content', () => {
    render(<MarkdownBlockquote {...defaultProps} />)

    const blockquote = screen.getByText('This is a blockquote')

    expect(blockquote).toBeInTheDocument()
    expect(blockquote).toHaveStyle({
      paddingTop: '14px',
      paddingBottom: '2px',
      paddingLeft: mockThemeToken.padding,
      paddingRight: mockThemeToken.padding,
      borderLeft: `${mockThemeToken.lineWidthBold}px solid ${mockThemeToken.colorPrimary}`,
      background: mockThemeToken.colorFillTertiary,
    })
  })

  it('renders a blockquote element so assistive technology announces a quote', () => {
    // Arrange / Act
    render(<MarkdownBlockquote {...defaultProps} />)

    // Assert
    expect(screen.getByRole('blockquote')).toHaveTextContent(
      'This is a blockquote',
    )
  })

  it('applies additional styles from props', () => {
    const customStyle = { color: 'red' }
    render(<MarkdownBlockquote {...defaultProps} style={customStyle} />)

    const blockquote = screen.getByText('This is a blockquote')
    expect(blockquote).toHaveStyle({ color: 'rgb(255, 0, 0)' })
  })

  it('renders child elements inside the blockquote', () => {
    render(
      <MarkdownBlockquote {...defaultProps}>
        <span>Nested child</span>
      </MarkdownBlockquote>,
    )

    const child = screen.getByText('Nested child')
    expect(child).toBeInTheDocument()
  })

  it('does not render when children contain only whitespace', () => {
    const { container } = render(
      <MarkdownBlockquote token={mockThemeToken}>{'    '}</MarkdownBlockquote>,
    )
    expect(container.firstChild).toBeNull()
  })
})
