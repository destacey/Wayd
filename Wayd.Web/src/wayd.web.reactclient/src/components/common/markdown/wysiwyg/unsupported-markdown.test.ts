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
      '[^a]: The note.',
    ].join('\n')

    // Act
    const found = findUnsupportedMarkdown(markdown)

    // Assert
    expect(found).toEqual([
      'footnotes',
      'collapsible sections',
      'keyboard keys',
      'HTML comments',
      'HTML blocks',
      'special characters written as HTML entities',
    ])
  })

  it.each([
    ['plain text', 'Deepen data & analytics.'],
    ['the entities the editor decodes', '&lt;b&gt; &amp; &quot;'],
    ['a numeric entity', 'Copyright &#169;'],
    ['a task list', '- [ ] todo\n- [x] done'],
    ['a link', '[Wayd](https://wayd.dev)'],
    ['underline, sub and sup', '<u>u</u> H<sub>2</sub>O x<sup>2</sup>'],
    [
      'constructs inside inline code',
      'Write `<details>` or `[^1]` or `&nbsp;`',
    ],
    ['constructs inside a fenced block', '```html\n<details>\n<!-- c -->\n```'],
    ['nothing', undefined],
  ])('finds nothing in %s', (_, markdown) => {
    // Arrange / Act / Assert
    expect(findUnsupportedMarkdown(markdown)).toEqual([])
  })
})
