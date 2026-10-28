import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import MarkdownRenderer from './markdown-renderer'

// jest.setup.ts stubs these globally; this suite renders through the real pipeline so the plugin
// order and sanitizer are what is under test.
jest.unmock('react-markdown')
jest.unmock('remark-gfm')
jest.unmock('rehype-raw')

describe('MarkdownRenderer', () => {
  it('renders nothing when there is no markdown', () => {
    // Arrange / Act
    const { container } = render(<MarkdownRenderer markdown="" />)

    // Assert
    expect(container.firstChild).toBeNull()
  })

  it('parses allowed raw HTML instead of showing it as text', () => {
    // Arrange / Act
    const { container } = render(
      <MarkdownRenderer markdown="Some <u>underlined</u> text" />,
    )

    // Assert
    expect(screen.getByText('underlined')).toBeInTheDocument()
    expect(container).not.toHaveTextContent('<u>')
  })

  it('strips dangerous raw HTML and inline styles', () => {
    // Arrange
    const markdown = [
      'Before',
      '',
      '<iframe src="https://attacker.example"></iframe>',
      '',
      '<form action="https://attacker.example"><button>Go</button></form>',
      '',
      '<div style="position:fixed;top:0;left:0">overlay</div>',
    ].join('\n')

    // Act
    const { container } = render(<MarkdownRenderer markdown={markdown} />)

    // Assert
    expect(container.querySelector('iframe')).toBeNull()
    expect(container.querySelector('form')).toBeNull()
    expect(container.querySelector('[style*="fixed"]')).toBeNull()
    expect(screen.getByText('overlay')).toBeInTheDocument()
  })

  it('blanks javascript: links', () => {
    // Arrange / Act
    const { container } = render(
      <MarkdownRenderer markdown='<a href="javascript:alert(1)">click</a>' />,
    )

    // Assert
    expect(container.innerHTML).not.toContain('javascript:')
  })

  it('renders a fenced block as one code element carrying its language', () => {
    // Arrange
    const markdown = '```ts\nconst x = 1\n```'

    // Act
    const { container } = render(<MarkdownRenderer markdown={markdown} />)

    // Assert
    const codeElements = container.querySelectorAll('pre code')
    expect(codeElements).toHaveLength(1)
    expect(codeElements[0]).toHaveClass('language-ts')
    expect(codeElements[0]).toHaveTextContent('const x = 1')
    expect(container.querySelectorAll('code')).toHaveLength(1)
  })

  it('keeps a raw-HTML pre whose only child is not code intact', () => {
    // Arrange
    const markdown = '<pre><a href="https://example.com">a link</a></pre>'

    // Act
    const { container } = render(<MarkdownRenderer markdown={markdown} />)

    // Assert
    expect(screen.getByRole('link', { name: 'a link' })).toHaveAttribute(
      'href',
      'https://example.com',
    )
    expect(container.querySelector('pre code a')).not.toBeNull()
  })

  it('opens external links in a new tab and keeps in-page links in place', () => {
    // Arrange
    const markdown =
      '[site](https://example.com) [cdn](//cdn.example.com/x) [section](#details) [page](/ppm/projects) [mail](mailto:a@example.com)'

    // Act
    render(<MarkdownRenderer markdown={markdown} />)

    // Assert
    const external = screen.getByRole('link', { name: 'site' })
    expect(external).toHaveAttribute('target', '_blank')
    expect(external).toHaveAttribute('rel', 'noopener noreferrer')
    expect(screen.getByRole('link', { name: 'cdn' })).toHaveAttribute(
      'target',
      '_blank',
    )
    expect(screen.getByRole('link', { name: 'section' })).not.toHaveAttribute(
      'target',
    )
    expect(screen.getByRole('link', { name: 'page' })).not.toHaveAttribute(
      'target',
    )
    expect(screen.getByRole('link', { name: 'mail' })).not.toHaveAttribute(
      'target',
    )
  })

  it('renders GitHub-flavoured tables', () => {
    // Arrange
    const markdown = '| Column |\n| --- |\n| Cell |'

    // Act
    render(<MarkdownRenderer markdown={markdown} />)

    // Assert
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: 'Cell' })).toBeInTheDocument()
  })
})
