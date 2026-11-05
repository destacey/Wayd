import { render } from '@testing-library/react'
import MarkdownRenderer from '../markdown-renderer'

/**
 * How faithfully a round trip kept a piece of markdown, judged by what `MarkdownRenderer` shows.
 *
 * - `exact` — the text is byte for byte the same.
 * - `normalized` — the text changed but renders to the same HTML.
 * - `formatting-lost` — the rendering changed but everything a reader saw, in order, is still there,
 *   and so is every link and image address.
 * - `content-lost` — something a reader saw is gone or out of order, or a link or image lost its
 *   address.
 */
export type Fidelity =
  'exact' | 'normalized' | 'formatting-lost' | 'content-lost'

/** Ordered best to worst, for summaries. */
export const fidelityOrder: readonly Fidelity[] = [
  'exact',
  'normalized',
  'formatting-lost',
  'content-lost',
]

interface Rendering {
  html: string
  /** Every visible non-whitespace character, in reading order. */
  characters: string[]
  /** Link targets, image sources and image alt text: content a reader reaches but does not read. */
  targets: string[]
  /** The exact text of each code block, where whitespace is content. */
  codeBlocks: string[]
}

const renderMarkdown = (markdown: string): Rendering => {
  const { container, unmount } = render(
    <MarkdownRenderer markdown={markdown} />,
  )

  // Whitespace between tags is not something a reader sees, and serializers move it freely; nor is
  // a CR before a LF, which the browser drops when it lays out text. Code blocks, where it is
  // content, are compared separately.
  const html = container.innerHTML
    .replace(/\r\n/g, '\n')
    .replace(/>\s+</g, '><')
    .trim()

  // remark-gfm adds a hidden "Footnotes" heading and ↩ back-links. They are chrome, not content:
  // escaping a footnote loses them, and that must count as lost formatting, not lost words.
  const visible = container.cloneNode(true) as HTMLElement
  visible
    .querySelectorAll('.sr-only, [data-footnote-backref]')
    .forEach((element) => element.remove())

  const characters = [...(visible.textContent ?? '').replace(/\s/g, '')]
  // Footnote reference links are chrome too: they point at the footnote section, not anywhere.
  const targets = [
    ...visible.querySelectorAll('a[href]:not([data-footnote-ref]), img'),
  ].flatMap((element) =>
    ['href', 'src', 'alt']
      .map((name) => element.getAttribute(name))
      .filter((value): value is string => !!value),
  )
  const codeBlocks = [...container.querySelectorAll('pre')].map(
    (pre) => pre.textContent ?? '',
  )

  unmount()
  return { html, characters, targets, codeBlocks }
}

/** `expected` appears in `actual` in the same order, with anything allowed in between. */
const isSubsequence = (actual: string[], expected: string[]): boolean => {
  let position = 0
  for (const char of actual) {
    if (char === expected[position]) position++
  }
  return position === expected.length
}

/** Classifies a round trip of `original` that produced `output`. */
export const classifyFidelity = (
  original: string,
  output: string,
): Fidelity => {
  if (original === output) return 'exact'

  const before = renderMarkdown(original)
  const after = renderMarkdown(output)
  if (
    before.html === after.html &&
    before.codeBlocks.join('\0') === after.codeBlocks.join('\0')
  ) {
    return 'normalized'
  }

  const keptText = isSubsequence(after.characters, before.characters)
  const keptTargets = before.targets.every((target) =>
    after.targets.includes(target),
  )
  return keptText && keptTargets ? 'formatting-lost' : 'content-lost'
}
