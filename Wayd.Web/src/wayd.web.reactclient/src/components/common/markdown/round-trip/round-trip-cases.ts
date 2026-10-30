import type { Fidelity } from './classify-fidelity'

/**
 * What the editor must do about a difference:
 * - `accept` — harmless; let the editor write it once the user edits.
 * - `configure` — fix in the editor's extensions or serializer.
 * - `preserve` — cannot be fixed by configuration; keep the original text unless the user edits.
 */
export type Handling = 'accept' | 'configure' | 'preserve'

/** A hand-written piece of markdown the renderer supports, and how Tiptap's round trip treats it. */
export interface RoundTripCase {
  name: string
  markdown: string
  /** The fidelity the round trip has today. A change in either direction should be looked at. */
  expected: Fidelity
  handling: Handling
  /** Why it changes, for the results table. Empty when it survives exactly. */
  note?: string
}

/** The one input whose round trip is not stable: the second pass differs from the first. */
export const unstableCases = new Set(['fence containing a fence'])

export const roundTripCases: RoundTripCase[] = [
  // Paragraphs and text
  {
    name: 'plain sentence',
    markdown: "The portfolio's goals: one/two; three - four.",
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'unicode and smart quotes',
    markdown: 'Café — “quoted” ✅ 日本',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'long paragraph',
    markdown: 'Word '.repeat(60).trim(),
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'empty document',
    markdown: '',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'soft line break',
    markdown: 'line one\nline two',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'several blank lines',
    markdown: 'a\n\n\n\nb',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'trailing spaces',
    markdown: 'text   ',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'leading spaces',
    markdown: '   indented para',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'trailing newline',
    markdown: 'text\n',
    expected: 'normalized',
    handling: 'accept',
    note: 'Final newline dropped.',
  },
  {
    name: 'CRLF line endings',
    markdown: 'one\r\ntwo\r\n\r\nthree',
    expected: 'normalized',
    handling: 'preserve',
    note: 'CRLF becomes LF. Text from the API, MCP or imports can hold CRLF; a browser textarea never does.',
  },
  {
    name: 'dollar and percent',
    markdown: 'Cost $5 and 50%',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'emoji shortcode',
    markdown: ':rocket: launch',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: '#1 at line start',
    markdown: '#1 priority',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'year at line start',
    markdown: '2026. A good year',
    expected: 'exact',
    handling: 'accept',
  },

  // Characters the serializer escapes
  {
    name: 'ampersand',
    markdown: 'Deepen data & analytics.',
    expected: 'normalized',
    handling: 'configure',
    note: '`&` written as `&amp;`. Renders the same, but agents reading through MCP see the entity.',
  },
  {
    name: 'less-than and greater-than',
    markdown: 'Latency < 200ms and a > b',
    expected: 'normalized',
    handling: 'configure',
    note: '`<` and `>` written as `&lt;` / `&gt;`.',
  },
  {
    name: 'literal asterisks',
    markdown: '2 * 3 * 4',
    expected: 'normalized',
    handling: 'configure',
    note: 'Every `*` backslash-escaped, even where it could not start emphasis.',
  },
  {
    name: 'intraword underscores',
    markdown: 'snake_case_name',
    expected: 'normalized',
    handling: 'configure',
    note: '`snake\\_case\\_name`: GFM never treats intraword `_` as emphasis.',
  },
  {
    name: 'backslash escapes',
    markdown: '\\*not em\\* and \\_not\\_ and \\# not heading',
    expected: 'normalized',
    handling: 'accept',
    note: 'An unneeded `\\#` mid-line is dropped.',
  },
  {
    name: 'escaped entity',
    markdown: '&lt;tag&gt;',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'named and numeric entities',
    markdown: 'a&nbsp;b &copy; &#169;',
    expected: 'content-lost',
    handling: 'preserve',
    note: 'Only `&lt;` `&gt;` `&quot;` `&amp;` and numeric references are decoded; `&nbsp;` and `&copy;` are re-escaped and then show as literal text.',
  },

  // Emphasis
  {
    name: 'star bold and italic',
    markdown: '**bold** and *em*',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'underscore bold and italic',
    markdown: '__bold__ and _em_',
    expected: 'normalized',
    handling: 'accept',
    note: '`__`/`_` become `**`/`*`.',
  },
  {
    name: 'bold italic',
    markdown: '***both***',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'strikethrough',
    markdown: '~~gone~~',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'single-tilde strikethrough',
    markdown: '~gone~',
    expected: 'normalized',
    handling: 'accept',
    note: '`~` becomes `~~`.',
  },
  {
    name: 'link inside bold',
    markdown: '**[x](https://e.x)**',
    expected: 'formatting-lost',
    handling: 'accept',
    note: 'Rewritten as `[**x**](…)`: the same look, with `<a>` outside `<strong>`.',
  },
  {
    name: 'emphasis inside link',
    markdown: '[*x*](https://e.x)',
    expected: 'exact',
    handling: 'accept',
  },

  // Headings and rules
  {
    name: 'setext heading',
    markdown: 'Title\n=====',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes `# Title`.',
  },
  {
    name: 'closed ATX heading',
    markdown: '## Title ##',
    expected: 'normalized',
    handling: 'accept',
    note: 'Closing `##` dropped.',
  },
  {
    name: 'level-six heading',
    markdown: '###### Six',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'star rule',
    markdown: 'a\n\n***\n\nb',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes `---`.',
  },
  {
    name: 'underscore rule',
    markdown: 'a\n\n___\n\nb',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes `---`.',
  },

  // Lists
  {
    name: 'dash bullets',
    markdown: '- one\n- two',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'star bullets',
    markdown: '* one\n* two',
    expected: 'normalized',
    handling: 'preserve',
    note: '`*` becomes `-`. The case the epic names: common in existing content.',
  },
  {
    name: 'plus bullets',
    markdown: '+ one\n+ two',
    expected: 'normalized',
    handling: 'preserve',
    note: '`+` becomes `-`.',
  },
  {
    name: 'ordered list from 3',
    markdown: '3. three\n4. four',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'parenthesis ordered list',
    markdown: '1) one\n2) two',
    expected: 'normalized',
    handling: 'accept',
    note: '`1)` becomes `1.`.',
  },
  {
    name: 'nested list and quote',
    markdown: '1. a\n   - b\n     > q\n2. c',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'loose list',
    markdown: '- one\n\n- two',
    expected: 'formatting-lost',
    handling: 'preserve',
    note: 'Made tight: the blank line between items, and the paragraph spacing it gives, is lost.',
  },

  // Task lists
  {
    name: 'task list',
    markdown: '- [ ] todo\n- [x] done',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'nested task list',
    markdown: '- [ ] parent\n  - [x] child',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'task and bullet in one list',
    markdown: '- [ ] task\n- plain',
    expected: 'formatting-lost',
    handling: 'accept',
    note: 'Split into two lists with a blank line between.',
  },

  // Quotes
  {
    name: 'nested quote',
    markdown: '> a\n>\n> > b',
    expected: 'exact',
    handling: 'accept',
  },

  // Code
  {
    name: 'fenced code with language',
    markdown: '```ts\nconst a = 1\n```',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'fenced code without language',
    markdown: '```\nplain\n```',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'tilde fence',
    markdown: '~~~sql\nselect 1\n~~~',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes a backtick fence.',
  },
  {
    name: 'indented code',
    markdown: 'Para\n\n    indented code',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes a fenced block.',
  },
  {
    name: 'fence containing a fence',
    markdown: '````md\n```js\nx\n```\n````',
    expected: 'content-lost',
    handling: 'configure',
    note: 'Always written with three backticks, so an inner fence closes the block early. Unstable: each save changes it again.',
  },
  {
    name: 'inline code with a backtick',
    markdown: 'Use `` a`b `` here',
    expected: 'normalized',
    handling: 'accept',
    note: 'Padding spaces inside the double backticks dropped.',
  },
  {
    name: 'inline code with HTML characters',
    markdown: 'Run `a & b <c>`',
    expected: 'exact',
    handling: 'accept',
  },

  // Tables
  {
    name: 'table',
    markdown: '| Name | Owner |\n| --- | --- |\n| Alpha | Ana |',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Columns padded to equal width, and blank lines added around the table.',
  },
  {
    name: 'aligned table with inline formatting',
    markdown:
      '| a | b | c |\n|:--|:-:|--:|\n| **x** | `y` | [l](https://e.x) |',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Padded; alignment kept.',
  },
  {
    name: 'table with escaped pipe',
    markdown: '| a |\n| --- |\n| x \\| y |',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Padded; escaped pipe kept.',
  },
  {
    name: 'table without outer pipes',
    markdown: 'a | b\n--- | ---\n1 | 2',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Outer pipes added.',
  },

  // Links and images
  {
    name: 'link with title',
    markdown: '[Wayd](https://wayd.dev "Home")',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'mailto link',
    markdown: '[mail](mailto:a@b.c)',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'angle-bracket autolink',
    markdown: '<https://wayd.dev>',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes `[url](url)`.',
  },
  {
    name: 'bare URL',
    markdown: 'See https://wayd.dev now',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Becomes `[url](url)`: every bare URL in a record is rewritten.',
  },
  {
    name: 'reference link',
    markdown: '[Wayd][w]\n\n[w]: https://wayd.dev',
    expected: 'normalized',
    handling: 'accept',
    note: 'Inlined; the definition is removed.',
  },
  {
    name: 'image',
    markdown: '![Logo](https://wayd.dev/logo.png)',
    expected: 'formatting-lost',
    handling: 'configure',
    note: 'StarterKit has no image node, so only the alt text is kept. Add `@tiptap/extension-image`.',
  },

  // Breaks
  {
    name: 'hard break with two spaces',
    markdown: 'line one  \nline two',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'hard break with backslash',
    markdown: 'line one\\\nline two',
    expected: 'normalized',
    handling: 'accept',
    note: 'Becomes two trailing spaces.',
  },

  // Raw HTML the sanitizer allows
  {
    name: 'underline',
    markdown: 'a <u>under</u> b',
    expected: 'exact',
    handling: 'accept',
  },
  {
    name: 'subscript and superscript',
    markdown: 'H<sub>2</sub>O and x<sup>2</sup>',
    expected: 'formatting-lost',
    handling: 'configure',
    note: 'Tags dropped. `@tiptap/extension-subscript` / `-superscript` keep them.',
  },
  {
    name: 'keyboard key',
    markdown: 'Press <kbd>Ctrl</kbd>',
    expected: 'formatting-lost',
    handling: 'preserve',
    note: 'Tag dropped.',
  },
  {
    name: 'details and summary',
    markdown: '<details><summary>More</summary>\n\nHidden text\n\n</details>',
    expected: 'formatting-lost',
    handling: 'preserve',
    note: 'Collapsible section flattened into paragraphs.',
  },
  {
    name: 'br tag',
    markdown: 'one<br>two',
    expected: 'formatting-lost',
    handling: 'accept',
    note: 'Becomes a markdown hard break. Looks the same; the class comes from the newline it adds after `<br>`.',
  },
  {
    name: 'aligned div',
    markdown: '<div align="center">centred</div>',
    expected: 'formatting-lost',
    handling: 'preserve',
    note: 'Wrapper dropped.',
  },
  {
    name: 'HTML comment',
    markdown: 'Visible <!-- hidden --> text',
    expected: 'normalized',
    handling: 'preserve',
    note: 'Comment deleted. Never displayed, but an author may rely on it.',
  },
  {
    name: 'script tag',
    markdown: 'safe <script>alert(1)</script> text',
    expected: 'normalized',
    handling: 'accept',
    note: 'Removed, as the renderer already does.',
  },

  // GFM extensions without a Tiptap node
  {
    name: 'footnote',
    markdown: 'Note[^1]\n\n[^1]: The footnote.',
    expected: 'formatting-lost',
    handling: 'preserve',
    note: 'Escaped to literal `\\[^1\\]` text; the footnote section is lost.',
  },
]
