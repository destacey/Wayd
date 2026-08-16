import { sanitizeSchema } from './sanitize-schema'

// jest.setup.ts mocks react-markdown, remark-gfm and rehype-raw globally, so a component-level test
// of <MarkdownRenderer/> renders a stub and proves nothing about sanitization. These tests exercise
// the schema against the real sanitizer instead.
//
// Only `hast-util-sanitize` (rehype-sanitize's own engine) and `hast-util-from-html` are used, both
// reachable from declared dependencies — the surrounding remark/unified packages are not in
// package.json and importing them here would break on any hoisting change.
const parseAndSanitize = async (html: string): Promise<string> => {
  const { fromHtml } = await import('hast-util-from-html')
  const { sanitize } = await import('hast-util-sanitize')
  const { toHtml } = await import('hast-util-to-html')

  return toHtml(sanitize(fromHtml(html, { fragment: true }), sanitizeSchema))
}

describe('sanitizeSchema blocks injected HTML', () => {
  it.each([
    ['iframe', '<iframe src="https://attacker.example"></iframe>', '<iframe'],
    ['form', '<form action="https://attacker.example"><button>Go</button></form>', '<form'],
    ['base', '<base href="https://attacker.example">', '<base'],
    ['meta refresh', '<meta http-equiv="refresh" content="0;url=https://attacker.example">', '<meta'],
    ['style', '<style>body{display:none}</style>', '<style'],
    ['object', '<object data="x"></object>', '<object'],
    ['embed', '<embed src="x">', '<embed'],
    ['script', '<script>alert(1)</script>', '<script'],
  ])('strips %s', async (_name, input, forbidden) => {
    // Arrange / Act
    const output = await parseAndSanitize(input)

    // Assert
    expect(output).not.toContain(forbidden)
  })

  it('strips event-handler attributes', async () => {
    // Arrange / Act
    const output = await parseAndSanitize('<img src="x" onerror="alert(1)">')

    // Assert
    expect(output).not.toContain('onerror')
  })

  it('strips the style attribute so injected HTML cannot overlay the page', async () => {
    // Arrange — a full-viewport overlay is a same-origin clickjacking primitive that CSP
    // frame-ancestors does not prevent.
    const input =
      '<u style="position:fixed;top:0;left:0;width:100vw;height:100vh">overlay</u>'

    // Act
    const output = await parseAndSanitize(input)

    // Assert
    expect(output).toContain('<u>')
    expect(output).not.toContain('style=')
  })

  it('blanks javascript: URLs on links', async () => {
    // Arrange / Act
    const output = await parseAndSanitize('<a href="javascript:alert(1)">click</a>')

    // Assert
    expect(output).not.toContain('javascript:')
  })
})

describe('sanitizeSchema preserves legitimate markdown output', () => {
  it('keeps <u>, which the default schema does not allow', async () => {
    // Arrange / Act
    const output = await parseAndSanitize('<u>underlined</u>')

    // Assert
    expect(output).toContain('<u>underlined</u>')
  })

  it('keeps the language class rehype emits for fenced code blocks', async () => {
    // Arrange — the shape remark-rehype produces for ```ts fences.
    const input = '<pre><code class="language-ts">const x = 1</code></pre>'

    // Act
    const output = await parseAndSanitize(input)

    // Assert
    expect(output).toContain('language-ts')
  })

  it('keeps tables, task-list checkboxes and inline formatting', async () => {
    // Arrange
    const input = [
      '<table><thead><tr><th>Column</th></tr></thead>',
      '<tbody><tr><td>Cell</td></tr></tbody></table>',
      '<input type="checkbox" checked disabled>',
      '<strong>bold</strong> <del>struck</del>',
      '<a href="https://example.com">a link</a>',
    ].join('')

    // Act
    const output = await parseAndSanitize(input)

    // Assert
    expect(output).toContain('<table>')
    expect(output).toContain('<td>Cell</td>')
    expect(output).toContain('type="checkbox"')
    expect(output).toContain('<strong>bold</strong>')
    expect(output).toContain('<del>struck</del>')
    expect(output).toContain('href="https://example.com"')
  })
})
