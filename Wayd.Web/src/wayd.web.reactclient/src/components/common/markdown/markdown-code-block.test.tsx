import type React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { MarkdownCodeBlock, MarkdownCodeBlockProps } from '.'

describe('MarkdownCodeBlock', () => {
  const mockThemeToken = {
    padding: '16px',
    colorFillTertiary: '#ffffff',
    borderRadius: '4px',
  } as any

  const defaultProps: MarkdownCodeBlockProps = {
    token: mockThemeToken,
    children: 'Sample code block',
  }

  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: {
        writeText: jest.fn().mockResolvedValue(undefined),
      },
    })
  })

  afterEach(() => {
    jest.clearAllMocks()
  })

  it('renders the component with the correct styles and children', () => {
    render(<MarkdownCodeBlock {...defaultProps} />)

    const codeElement = screen.getByText('Sample code block')
    expect(codeElement).toBeInTheDocument()
    expect(codeElement).toHaveStyle('font-family: monospace')
    expect(codeElement).toHaveStyle('font-size: 0.9em')
  })

  it('copies the code block content to the clipboard when the copy button is clicked', () => {
    // Arrange
    render(
      <MarkdownCodeBlock {...defaultProps}>
        <code className="language-ts">
          <span className="token keyword">const</span> x = 1
        </code>
      </MarkdownCodeBlock>,
    )

    // Act
    fireEvent.click(
      screen.getByRole('button', { name: 'Copy code to clipboard' }),
    )

    // Assert
    expect(navigator.clipboard.writeText).toHaveBeenCalledTimes(1)
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('const x = 1')
  })

  it('lifts the content and language class out of the inner code element', () => {
    // Arrange
    const InlineCode = ({
      node: _node,
      ...props
    }: React.HTMLAttributes<HTMLElement> & { node?: unknown }) => (
      <code data-inline-style {...props} />
    )

    // Act
    const { container } = render(
      <MarkdownCodeBlock {...defaultProps}>
        <InlineCode className="language-ts" node={{ tagName: 'code' }}>
          <span className="token keyword">const</span> x
        </InlineCode>
      </MarkdownCodeBlock>,
    )

    // Assert
    const codeElements = container.querySelectorAll('code')
    expect(codeElements).toHaveLength(1)
    expect(codeElements[0]).toHaveClass('language-ts')
    expect(codeElements[0]).not.toHaveAttribute('data-inline-style')
    expect(container.querySelector('code span')).toHaveClass('token keyword')
  })

  it('applies additional styles passed via props', () => {
    const customStyle = { color: 'red' }
    render(
      <MarkdownCodeBlock {...defaultProps} style={customStyle}>
        Custom styled code
      </MarkdownCodeBlock>,
    )

    const codeElement = screen.getByText('Custom styled code')
    expect(codeElement).toHaveStyle('color: rgb(255, 0, 0)')
  })

  it('does not render when children are absent', () => {
    const { container } = render(<MarkdownCodeBlock token={mockThemeToken} />)
    expect(container.firstChild).toBeNull()
  })
})
