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
import {
  ATTACHMENT_SRC_ATTRIBUTE,
  attachmentIdFrom,
  isAttachmentPath,
  loadAttachmentUrl,
} from '../attachments'
import { bulletToTaskInputRule } from './bullet-to-task-input-rule'
import { installMinimalMarkdownEscaping } from './markdown-escaping'
import { markdownImageInputRule } from './markdown-image-input-rule'
import {
  acceptsAttachmentImages,
  MarkdownImageUpload,
} from './markdown-image-upload'
import { MarkdownLengthLimit } from './markdown-length-limit'
import { markdownLinkInputRule } from './markdown-link-input-rule'
import { MarkdownPaste } from './markdown-paste'
import { MarkdownTableShortcut } from './markdown-table-shortcut'
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
 * Typing `[text](address)` makes one too.
 */
const MarkdownLink = Link.extend({
  addInputRules() {
    return [...(this.parent?.() ?? []), markdownLinkInputRule()]
  },
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
 * taken from pasted HTML or typed as `![alt](address)`. The address is written as `MarkdownLink`
 * writes one.
 */
const MarkdownImage = Image.extend({
  addInputRules() {
    const { editor } = this
    return [markdownImageInputRule(() => acceptsAttachmentImages(editor))]
  },
  // An uploaded image's address is a path on the API, so anything that renders it as HTML — the first
  // draw before the node view is installed, a copy to the clipboard — keeps it out of `src`, where
  // the browser would ask this site for it.
  addAttributes() {
    return {
      ...this.parent?.(),
      src: {
        default: null,
        parseHTML: (element) =>
          element.getAttribute('src') ??
          element.getAttribute(ATTACHMENT_SRC_ATTRIBUTE),
        renderHTML: (attributes) =>
          isAttachmentPath(attributes.src)
            ? { [ATTACHMENT_SRC_ATTRIBUTE]: attributes.src }
            : { src: attributes.src },
      },
    }
  },
  // An uploaded image is loaded through the API: as a plain `src` it would be asked of this site.
  addNodeView() {
    return ({ node }) => {
      const dom = document.createElement('img')
      let shown: string | null = null
      const show = (src: string, alt: string, title: string | null) => {
        dom.alt = alt
        if (title) dom.title = title
        else dom.removeAttribute('title')
        if (src === shown) return
        shown = src
        const id = attachmentIdFrom(src)
        if (!id) {
          dom.src = src
          return
        }
        dom.removeAttribute('src')
        loadAttachmentUrl(id).then(
          (url) => {
            if (shown === src) dom.src = url
          },
          () => {
            if (shown === src) dom.alt = `[${alt || 'Image'} unavailable]`
          },
        )
      }
      show(node.attrs.src ?? '', node.attrs.alt ?? '', node.attrs.title)
      return {
        dom,
        update: (updated) => {
          if (updated.type !== node.type) return false
          show(
            updated.attrs.src ?? '',
            updated.attrs.alt ?? '',
            updated.attrs.title,
          )
          return true
        },
      }
    }
  },
  parseHTML() {
    return [
      {
        tag: 'img[src]',
        getAttrs: (element) =>
          isSafeImageSrc((element as HTMLElement).getAttribute('src'))
            ? null
            : false,
      },
      {
        tag: `img[${ATTACHMENT_SRC_ATTRIBUTE}]`,
        getAttrs: (element) =>
          isAttachmentPath(
            (element as HTMLElement).getAttribute(ATTACHMENT_SRC_ATTRIBUTE),
          )
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

/** A task item that a bullet also turns into when its line starts with `[ ] ` or `[x] `. */
const MarkdownTaskItem = TaskItem.extend({
  addInputRules() {
    return [...(this.parent?.() ?? []), bulletToTaskInputRule()]
  },
}).configure({ nested: true })

/**
 * The only extensions whose paste rules run: pasted URLs become links. The bold, italic, strike and
 * code paste rules would format `2 * 3 * 4` in any paste, even prose and Ctrl+Shift+V; markdown
 * pasted as plain text is formatted by `MarkdownPaste` instead.
 */
export const markdownEditorPasteRules = ['link']

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
  MarkdownTaskItem,
  MarkdownImage,
  MarkdownSubscript,
  MarkdownSuperscript,
  MarkdownLengthLimit,
  MarkdownPaste,
  MarkdownImageUpload,
  MarkdownTableShortcut,
  Markdown,
]
