import type { Extensions, MarkdownToken } from '@tiptap/core'
import CodeBlock from '@tiptap/extension-code-block'
import Image from '@tiptap/extension-image'
import Link from '@tiptap/extension-link'
import { TaskItem, TaskList } from '@tiptap/extension-list'
import Subscript from '@tiptap/extension-subscript'
import Superscript from '@tiptap/extension-superscript'
import { TableKit } from '@tiptap/extension-table'
import { Markdown } from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'
import { installMinimalMarkdownEscaping } from './markdown-escaping'
import { MarkdownLengthLimit } from './markdown-length-limit'
import {
  isSafeImageSrc,
  isSafeLinkHref,
  toMarkdownDestination,
} from './safe-urls'

installMinimalMarkdownEscaping()

/**
 * A code block written with a fence longer than any backtick run inside it. Tiptap always writes
 * three backticks, so a block that contains a fence (a markdown example) would be closed early.
 */
const FencedCodeBlock = CodeBlock.extend({
  renderMarkdown: (node, helpers) => {
    const language = node.attrs?.language || ''
    const body = node.content ? helpers.renderChildren(node.content) : ''
    const longestRun = Math.max(
      0,
      ...(body.match(/`+/g) ?? []).map((run) => run.length),
    )
    const fence = '`'.repeat(Math.max(3, longestRun + 1))
    return `${fence}${language}\n${body}\n${fence}`
  },
})

/** Kept as raw `<sub>` HTML, which `sanitizeSchema` allows; markdown has no syntax for it. */
const MarkdownSubscript = Subscript.extend({
  renderMarkdown: (node, helpers) =>
    `<sub>${helpers.renderChildren(node)}</sub>`,
})

/** Kept as raw `<sup>` HTML, which `sanitizeSchema` allows; markdown has no syntax for it. */
const MarkdownSuperscript = Superscript.extend({
  renderMarkdown: (node, helpers) =>
    `<sup>${helpers.renderChildren(node)}</sup>`,
})

/** A link title in quotes, with backslashes and quotes inside it escaped. */
const markdownTitle = (title: string | null | undefined) =>
  title ? ` "${title.replace(/[\\"]/g, '\\$&')}"` : ''

/**
 * A link whose address is written so markdown reads it back whole: Tiptap writes `[text](href)`
 * as is, so a space or `)` in the address (pasted, or typed in the link dialog) would end it early.
 */
const MarkdownLink = Link.extend({
  renderMarkdown: (node, helpers) =>
    `[${helpers.renderChildren(node)}](${toMarkdownDestination(node.attrs?.href ?? '')}${markdownTitle(node.attrs?.title)})`,
}).configure({
  // Clicking inside the editor places the cursor; following a link would leave the form.
  openOnClick: false,
  defaultProtocol: 'https',
  isAllowedUri: (url) => isSafeLinkHref(url),
  HTMLAttributes: { target: '_blank', rel: 'noopener noreferrer nofollow' },
})

/**
 * The plain text of inline markdown tokens, with escapes resolved. An image token's own `text`
 * resolves `\[` but leaves `\\` doubled, so alt text read from it would grow on every save.
 */
const plainText = (tokens: MarkdownToken[]): string =>
  tokens
    .map((token) =>
      token.tokens ? plainText(token.tokens) : (token.text ?? ''),
    )
    .join('')

/**
 * Images that already sit in the text, inline as markdown writes them, with only `http(s)` sources
 * taken from pasted HTML. The address is written as `MarkdownLink` writes one.
 */
const MarkdownImage = Image.extend({
  parseHTML() {
    return [
      {
        tag: 'img[src]',
        getAttrs: (element) =>
          isSafeImageSrc((element as HTMLElement).getAttribute('src'))
            ? null
            : false,
      },
    ]
  },
  parseMarkdown: (token, helpers) =>
    helpers.createNode('image', {
      src: token.href,
      title: token.title,
      alt: token.tokens ? plainText(token.tokens) : token.text,
    }),
  renderMarkdown: (node) => {
    const alt = (node.attrs?.alt ?? '').replace(/[\\[\]]/g, '\\$&')
    return `![${alt}](${toMarkdownDestination(node.attrs?.src ?? '')}${markdownTitle(node.attrs?.title)})`
  },
}).configure({ inline: true })

/**
 * The extensions `MarkdownEditor` edits with. The round-trip harness measures this exact set, so a
 * change here is a change to what it proves.
 */
export const markdownEditorExtensions = (): Extensions => [
  StarterKit.configure({
    codeBlock: false,
    link: false,
  }),
  MarkdownLink,
  FencedCodeBlock,
  TableKit,
  TaskList,
  TaskItem.configure({ nested: true }),
  MarkdownImage,
  MarkdownSubscript,
  MarkdownSuperscript,
  MarkdownLengthLimit,
  Markdown,
]
