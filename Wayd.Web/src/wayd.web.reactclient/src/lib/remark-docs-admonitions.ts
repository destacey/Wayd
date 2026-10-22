/**
 * Remark plugin that renders Docusaurus admonitions (`:::note`, `:::warning[Title]`, ...) in the
 * in-app docs, so the shared `docs/` folder reads the same here as on the docs site.
 *
 * Must run after `remark-directive`, which parses the `:::` syntax into directive nodes.
 *
 * `remark-directive` also parses `:name` and `::name` anywhere in prose. Docs never use those as
 * directives, so they — and any `:::` block with an unknown name — are put back as the text
 * they were written as, which is what Docusaurus does too.
 */
import { visit, SKIP } from 'unist-util-visit'
import type { Parent, Root, RootContent } from 'mdast'
import type { ContainerDirective, LeafDirective, TextDirective } from 'mdast-util-directive'
import type { VFile } from 'vfile'

/** The admonition types Docusaurus supports, with the title each shows when none is given. */
export const ADMONITION_TITLES: Record<string, string> = {
  note: 'Note',
  tip: 'Tip',
  info: 'Info',
  warning: 'Warning',
  caution: 'Caution',
  danger: 'Danger',
}

type Directive = ContainerDirective | LeafDirective | TextDirective

function sourceText(node: Directive, file: VFile): string {
  const start = node.position?.start.offset
  const end = node.position?.end.offset
  if (start !== undefined && end !== undefined && typeof file.value === 'string') {
    return file.value.slice(start, end)
  }
  return `:${node.name}`
}

export default function remarkDocsAdmonitions() {
  return (tree: Root, file: VFile) => {
    visit(tree, (visited, index, parent: Parent | undefined) => {
      if (
        visited.type !== 'containerDirective' &&
        visited.type !== 'leafDirective' &&
        visited.type !== 'textDirective'
      ) {
        return
      }
      if (!parent || index === undefined) return
      const node = visited as Directive

      const defaultTitle = ADMONITION_TITLES[node.name]
      if (node.type !== 'containerDirective' || !defaultTitle) {
        const text = sourceText(node, file)
        const replacement: RootContent =
          node.type === 'textDirective'
            ? { type: 'text', value: text }
            : { type: 'paragraph', children: [{ type: 'text', value: text }] }
        parent.children.splice(index, 1, replacement)
        return [SKIP, index]
      }

      const first = node.children[0]
      const hasLabel = first?.type === 'paragraph' && first.data?.directiveLabel === true
      const titleChildren = hasLabel ? first.children : [{ type: 'text' as const, value: defaultTitle }]
      const body = hasLabel ? node.children.slice(1) : node.children

      node.data = {
        hName: 'div',
        hProperties: { className: ['docs-admonition', `docs-admonition-${node.name}`] },
      }
      node.children = [
        {
          type: 'paragraph',
          data: { hName: 'div', hProperties: { className: ['docs-admonition-title'] } },
          children: titleChildren,
        },
        ...body,
      ]
    })
  }
}
