import { render } from '@testing-library/react'
import MarkdownRenderer from '../markdown-renderer'

/**
 * How faithfully a round trip kept a piece of markdown, judged by what `MarkdownRenderer` shows.
 *
 * - `exact` — the text is byte for byte the same.
 * - `normalized` — the text changed but renders to the same HTML.
 * - `formatting-lost` — the rendering changed but every character a reader saw is still there.
 * - `content-lost` — something a reader saw is gone.
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
  /** Every visible non-whitespace character, so a lost `&` counts as much as a lost word. */
  characters: string[]
}

const renderMarkdown = (markdown: string): Rendering => {
  const { container, unmount } = render(
    <MarkdownRenderer markdown={markdown} />,
  )

  // Whitespace between tags is not something a reader sees, and serializers move it freely; nor is
  // a CR before a LF, which the browser drops when it lays out text.
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

  unmount()
  return { html, characters }
}

/** Every character of `expected` is present in `actual`, counting repeats. */
const containsAll = (actual: string[], expected: string[]): boolean => {
  const remaining = new Map<string, number>()
  for (const char of actual) remaining.set(char, (remaining.get(char) ?? 0) + 1)
  return expected.every((char) => {
    const count = remaining.get(char) ?? 0
    remaining.set(char, count - 1)
    return count > 0
  })
}

/** Classifies a round trip of `original` that produced `output`. */
export const classifyFidelity = (
  original: string,
  output: string,
): Fidelity => {
  if (original === output) return 'exact'

  const before = renderMarkdown(original)
  const after = renderMarkdown(output)
  if (before.html === after.html) return 'normalized'

  return containsAll(after.characters, before.characters)
    ? 'formatting-lost'
    : 'content-lost'
}
