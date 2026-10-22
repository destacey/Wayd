import { unified } from 'unified'
import remarkParse from 'remark-parse'
import remarkDirective from 'remark-directive'
import remarkRehype from 'remark-rehype'
import rehypeStringify from 'rehype-stringify'
import remarkDocsAdmonitions from './remark-docs-admonitions'

async function render(markdown: string): Promise<string> {
  const file = await unified()
    .use(remarkParse)
    .use(remarkDirective)
    .use(remarkDocsAdmonitions)
    .use(remarkRehype)
    .use(rehypeStringify)
    .process(markdown)
  return String(file)
}

describe('remarkDocsAdmonitions', () => {
  it('renders an untitled admonition with the default title for its type', async () => {
    // Arrange
    const markdown = ':::note\nBody text.\n:::'

    // Act
    const html = await render(markdown)

    // Assert
    expect(html).toContain('<div class="docs-admonition docs-admonition-note">')
    expect(html).toContain('<div class="docs-admonition-title">Note</div>')
    expect(html).toContain('<p>Body text.</p>')
  })

  it('renders a bracketed label as the title, keeping its inline markup', async () => {
    // Arrange
    const markdown = ':::warning[Drive off `isLoading`]\nBody text.\n:::'

    // Act
    const html = await render(markdown)

    // Assert
    expect(html).toContain('docs-admonition-warning')
    expect(html).toContain(
      '<div class="docs-admonition-title">Drive off <code>isLoading</code></div>',
    )
    expect(html).not.toContain(':::')
  })

  it('leaves an unknown container directive as the text it was written as', async () => {
    // Arrange
    const markdown = ':::unknown\nBody\n:::'

    // Act
    const html = await render(markdown)

    // Assert
    expect(html).not.toContain('docs-admonition')
    expect(html).toContain(':::unknown')
  })

  it('leaves colons in prose untouched', async () => {
    // Arrange
    const markdown = 'Format is key:value, see section:intro[here] at 10:30.'

    // Act
    const html = await render(markdown)

    // Assert
    expect(html).toBe(
      '<p>Format is key:value, see section:intro[here] at 10:30.</p>',
    )
  })
})
