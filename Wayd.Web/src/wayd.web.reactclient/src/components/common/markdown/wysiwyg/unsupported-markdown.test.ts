import { findUnsupportedMarkdown } from './unsupported-markdown'

describe('findUnsupportedMarkdown', () => {
  it('names every unsupported construct present, in a fixed order', () => {
    // Arrange
    const markdown = [
      'Text with a note[^a] and &nbsp; here.',
      '',
      '<!-- reviewer note -->',
      '',
      '<details><summary>More</summary>Hidden</details>',
      '',
      'Press <kbd>Esc</kbd> inside <div align="center">this</div>.',
      '',
      '<img src="/files/diagram.png" alt="diagram">',
      '',
      '[^a]: The note.',
    ].join('\n')

    // Act
    const found = findUnsupportedMarkdown(markdown)

    // Assert
    expect(found).toEqual([
      'footnotes',
      'HTML comments',
      'HTML the editor does not support (<details>, <summary>, <kbd>, <div>)',
      'images that are not web addresses',
      'special characters written as HTML entities',
    ])
  })

  it.each([
    ['a tag with attributes', '<ins cite="x">added</ins>', '<ins>'],
    ['a tag inline in a sentence', 'Use <q>quotes</q> here', '<q>'],
    [
      'a tag the editor would show as literal text',
      'Old <tt>mono</tt>',
      '<tt>',
    ],
  ])('reports %s', (_, markdown, tag) => {
    // Arrange / Act
    const found = findUnsupportedMarkdown(markdown)

    // Assert
    expect(found).toEqual([`HTML the editor does not support (${tag})`])
  })

  it('reports text after a stray backtick, which opens no code span', () => {
    // Arrange: one backtick, then a blank line, then HTML, then a real code span.
    const markdown =
      'Use a ` mark.\n\n<details><summary>S</summary>Hidden</details>\n\nAnd `x`'

    // Act
    const found = findUnsupportedMarkdown(markdown)

    // Assert
    expect(found).toEqual([
      'HTML the editor does not support (<details>, <summary>)',
    ])
  })

  it.each([
    ['plain text', 'Deepen data & analytics.'],
    ['the entities the editor decodes', '&lt;b&gt; &amp; &quot;'],
    ['a numeric entity', 'Copyright &#169;'],
    ['a task list', '- [ ] todo\n- [x] done'],
    ['a link', '[Wayd](https://wayd.dev)'],
    [
      'tags the editor keeps',
      '<u>u</u> H<sub>2</sub>O <b>b</b> <em>e</em> <br>',
    ],
    [
      'block HTML the editor converts',
      '<h2>Title</h2>\n\n<ul><li>one</li></ul>',
    ],
    ['a web image in raw HTML', '<img src="https://wayd.dev/a.png" alt="a">'],
    ['tags the sanitizer strips anyway', 'safe <script>alert(1)</script>'],
    ['a footnote reference with no definition', 'Use [^0-9] to match'],
    [
      'constructs inside inline code',
      'Write `<details>` or `[^1]` or `&nbsp;`',
    ],
    ['constructs inside double backticks', 'Write `` <kbd>`x` `` here'],
    ['constructs inside a fenced block', '```html\n<details>\n<!-- c -->\n```'],
    ['nothing', undefined],
  ])('finds nothing in %s', (_, markdown) => {
    // Arrange / Act / Assert
    expect(findUnsupportedMarkdown(markdown)).toEqual([])
  })
})
