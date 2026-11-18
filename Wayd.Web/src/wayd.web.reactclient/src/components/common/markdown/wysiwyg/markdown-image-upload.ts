import { type Editor, Extension } from '@tiptap/core'
import { Fragment, type Node as ProseMirrorNode, Slice } from '@tiptap/pm/model'
import {
  Plugin,
  PluginKey,
  type EditorState,
  type Transaction,
} from '@tiptap/pm/state'
import { Decoration, DecorationSet, type EditorView } from '@tiptap/pm/view'
import { errorDetail } from '@/src/utils/problem-details'
import {
  ATTACHMENT_SRC_ATTRIBUTE,
  attachmentPathForObjectUrl,
  dataUrlSize,
  imageFileFromDataUrl,
  imageFilesIn,
  imageUploadProblem,
  isAttachmentPath,
  MAX_IMAGE_BYTES,
} from '../attachments'
import { isSafeImageSrc } from './safe-urls'

/** Uploads one image, answering the path its markdown should refer to. */
export type ImageUploader = (file: File) => Promise<{ path: string }>

/** Mutable settings of `MarkdownImageUpload`, read on each paste or drop. */
export interface MarkdownImageUploadStorage {
  /** Uploads a pasted or dropped image; `undefined` leaves images to the editor's own handling. */
  upload: ImageUploader | undefined
  /** Told why an image could not be added, and told `null` as a new paste or drop starts. */
  onError: ((message: string | null) => void) | undefined
}

declare module '@tiptap/core' {
  interface Storage {
    markdownImageUpload: MarkdownImageUploadStorage
  }
}

type PlaceholderAction =
  { add: { id: object; pos: number }[] } | { remove: { id: object } }

/** An image embedded in pasted HTML, waiting for the paste to land so it can be uploaded where it sat. */
export interface EmbeddedImage {
  file: File
  alt: string
}

/** What a paste of HTML held besides what the editor parses itself. */
export interface PastedImages {
  /** `data:` images, each standing in the pasted HTML as the marker `markerFor` writes for its index. */
  embedded: EmbeddedImage[]
  /** Why images the paste left out could not be added, one message per reason. */
  problems: string[]
}

/**
 * Below this many bytes an embedded image is taken for a web page's spacer — a lazy-loading page's
 * 1×1 placeholder — and left out without a word.
 */
const SPACER_BYTES = 128

/**
 * Stands in for an embedded image in pasted HTML until the paste lands, so the image goes where it
 * sat. Object replacement characters around the index, which no one types.
 */
const markerFor = (index: number) => `\uFFFC${index}\uFFFC`
const MARKER = /\uFFFC(\d+)\uFFFC/g

/** Why `count` images in a paste were left out. */
const lostImagesMessage = (count: number): string =>
  `${count === 1 ? 'An image' : `${count} images`} in the pasted text could not be added. Copy and paste each image on its own.`

/** `messages` as one, each reason once: the editor shows a single message. */
export const joinProblems = (messages: string[]): string | null =>
  messages.length ? [...new Set(messages)].join(' ') : null

/** The address `img` shows from: its `src`, or where Wayd writes an uploaded image's path. */
const sourceOf = (img: Element): string | null =>
  img.getAttribute('src') ?? img.getAttribute(ATTACHMENT_SRC_ATTRIBUTE)

/**
 * The web address a lazy-loading page holds back in `data-src` or `srcset` while `src` shows a
 * placeholder, or `null` when it holds none.
 */
const lazySourceOf = (img: Element): string | null =>
  [
    img.getAttribute('data-src'),
    img.getAttribute('srcset')?.trim().split(/\s+/)[0],
  ].find((src) => isSafeImageSrc(src)) ?? null

/**
 * The address an image copied with `src` can be kept by: its own when the editor takes it, the path
 * of an image Wayd is showing from an object URL, or `null` when there is none.
 */
const keptSourceFor = (src: string): string | null =>
  isSafeImageSrc(src) ? src : attachmentPathForObjectUrl(src)

/**
 * Whether pasted `html` is only images whose addresses cannot be fetched, as when Word copies a
 * single picture or a browser copies an image it shows from memory. The clipboard then holds the
 * image itself as a file too, and that file is what to take.
 */
export const holdsOnlyUnreachableImages = (html: string): boolean => {
  const body = new DOMParser().parseFromString(html, 'text/html').body
  const images = Array.from(body.querySelectorAll('img'))
  return (
    images.length > 0 &&
    !body.textContent?.trim() &&
    images.every((img) => {
      const src = sourceOf(img)
      return (
        !src ||
        (keptSourceFor(src) === null &&
          lazySourceOf(img) === null &&
          !/^\s*data:/i.test(src))
      )
    })
  )
}

const placeholderKey = new PluginKey<DecorationSet>('markdownImageUpload')

/**
 * `html` with each image the editor would not keep as it is sorted out, and what was taken from it;
 * `null` when every image keeps its address.
 *
 * - An image Wayd shows from an object URL gets back its attachment path, and a lazy-loading page's
 *   image the web address it holds back.
 * - A `data:` image is replaced by its marker, to be uploaded once the paste lands; a spacer is left
 *   out, and one far over the size limit is refused before it is decoded.
 * - Any other image (`file://` from Word, another site's `blob:`) is left out and reported.
 */
export const takeImagesFromHtml = (
  html: string,
): { html: string; images: PastedImages } | null => {
  const parsed = new DOMParser().parseFromString(html, 'text/html')
  const images: PastedImages = { embedded: [], problems: [] }
  let changed = false
  let lost = 0
  for (const img of Array.from(parsed.body.querySelectorAll('img'))) {
    const src = sourceOf(img)
    if (src && isSafeImageSrc(src)) continue
    changed = true

    const kept = (src && keptSourceFor(src)) ?? lazySourceOf(img)
    if (kept) {
      img.setAttribute('src', kept)
      continue
    }

    const isData = !!src && /^\s*data:/i.test(src)
    if (isData && dataUrlSize(src) > MAX_IMAGE_BYTES * 1.5) {
      images.problems.push('Images can be at most 5 MB.')
      img.remove()
      continue
    }

    const file = isData
      ? imageFileFromDataUrl(src, `pasted-image-${images.embedded.length + 1}`)
      : null
    if (file && file.size < SPACER_BYTES) {
      img.remove()
    } else if (file) {
      images.embedded.push({ file, alt: img.getAttribute('alt') ?? '' })
      img.replaceWith(markerFor(images.embedded.length - 1))
    } else {
      img.remove()
      lost++
    }
  }
  if (lost) images.problems.push(lostImagesMessage(lost))

  return changed ? { html: parsed.body.innerHTML, images } : null
}

/** `fragment` without the images uploaded into another field, which this field could never claim. */
const withoutAttachmentImages = (fragment: Fragment): Fragment => {
  const kept: ProseMirrorNode[] = []
  fragment.forEach((node) => {
    if (node.type.name === 'image' && isAttachmentPath(node.attrs.src)) return
    kept.push(
      node.isLeaf ? node : node.copy(withoutAttachmentImages(node.content)),
    )
  })
  return Fragment.from(kept)
}

/** Whether `editor` takes uploaded images: only a field that claims them when its record is saved. */
export const acceptsAttachmentImages = (editor: Editor): boolean =>
  !!editor.storage.markdownImageUpload?.upload

/** A placeholder shown where an image is being uploaded to. */
const placeholderWidget = () => {
  const element = document.createElement('span')
  element.className = 'markdown-image-uploading'
  element.setAttribute('role', 'status')
  element.textContent = 'Uploading image…'
  return element
}

/**
 * The range `transactions` changed, in the document they end with; `null` when they changed nothing.
 */
const changedRange = (
  transactions: readonly Transaction[],
): { from: number; to: number } | null => {
  let range: { from: number; to: number } | null = null
  for (const tr of transactions) {
    if (range)
      range = {
        from: tr.mapping.map(range.from, -1),
        to: tr.mapping.map(range.to, 1),
      }
    tr.mapping.maps.forEach((map, index) => {
      const rest = tr.mapping.slice(index + 1)
      map.forEach((_oldStart, _oldEnd, start, end) => {
        const from = rest.map(start, -1)
        const to = rest.map(end, 1)
        range = range
          ? { from: Math.min(range.from, from), to: Math.max(range.to, to) }
          : { from, to }
      })
    })
  }
  return range
}

/** Where the placeholder `id` now sits, or `null` once the text around it was deleted. */
const placeholderPosition = (state: EditorState, id: object): number | null =>
  placeholderKey
    .getState(state)
    ?.find(undefined, undefined, (spec) => spec.id === id)[0]?.from ?? null

/**
 * Uploads images pasted or dropped into the document and inserts each where it was placed, with a
 * placeholder there meanwhile. Off until `setMarkdownImageUpload` gives it an uploader, so a field
 * whose record cannot hold images keeps the editor's own handling.
 *
 * A paste that carries HTML too is left to the editor: copying a table from a spreadsheet or an image
 * from a web page puts a picture of it on the clipboard beside the content the user meant to copy.
 * Images inside that HTML are still kept where they can be: one embedded as a `data:` URL is
 * uploaded where it sat in the text, and one whose address cannot be fetched (`file://` from Word,
 * `blob:`) is reported rather than dropped silently.
 */
export const MarkdownImageUpload = Extension.create<
  object,
  MarkdownImageUploadStorage
>({
  name: 'markdownImageUpload',

  addStorage() {
    return { upload: undefined, onError: undefined }
  },

  addProseMirrorPlugins() {
    const { storage } = this

    // Images taken from pasted HTML, until the paste lands.
    let pasted: PastedImages | null = null
    // Uploads to start, and refusals to report, once the paste that placed them is in the view.
    let afterPaste: {
      uploads: { id: object; file: File; alt: string }[]
      problems: string[]
    } | null = null

    /** Uploads `file` and replaces the placeholder `id` with it, wherever the placeholder now is. */
    const startUpload = (
      view: EditorView,
      id: object,
      file: File,
      alt: string,
    ) => {
      const upload = storage.upload
      if (!upload) return
      upload(file).then(
        ({ path }) => {
          if (view.isDestroyed) return
          const at = placeholderPosition(view.state, id)
          // Removed apart from the insert: the length limit can refuse the insert, and the placeholder
          // must not outlive it.
          view.dispatch(
            view.state.tr.setMeta(placeholderKey, {
              remove: { id },
            } satisfies PlaceholderAction),
          )
          if (at !== null) {
            view.dispatch(
              view.state.tr.insert(
                at,
                view.state.schema.nodes.image.create({ src: path, alt }),
              ),
            )
          }
        },
        (error: unknown) => {
          if (view.isDestroyed) return
          view.dispatch(
            view.state.tr.setMeta(placeholderKey, {
              remove: { id },
            } satisfies PlaceholderAction),
          )
          storage.onError?.(
            errorDetail(error) ?? 'The image could not be uploaded.',
          )
        },
      )
    }

    const uploadAll = (view: EditorView, files: File[], pos: number) => {
      if (!storage.upload) return
      storage.onError?.(null)
      for (const file of files) {
        const problem = imageUploadProblem(file)
        if (problem) {
          storage.onError?.(problem)
          continue
        }

        const id = {}
        view.dispatch(
          view.state.tr.setMeta(placeholderKey, {
            add: [{ id, pos }],
          } satisfies PlaceholderAction),
        )
        startUpload(view, id, file, '')
      }
    }

    return [
      new Plugin<DecorationSet>({
        key: placeholderKey,
        state: {
          init: () => DecorationSet.empty,
          apply: (tr, set) => {
            let next = set.map(tr.mapping, tr.doc)
            const action = tr.getMeta(placeholderKey) as
              PlaceholderAction | undefined
            if (action && 'add' in action) {
              next = next.add(
                tr.doc,
                // Before what is inserted at its position, so of two images placed at one position,
                // the one whose upload answers first does not jump ahead of the other's placeholder.
                action.add.map(({ id, pos }) =>
                  Decoration.widget(pos, placeholderWidget, { id, side: -1 }),
                ),
              )
            } else if (action && 'remove' in action) {
              next = next.remove(
                next.find(
                  undefined,
                  undefined,
                  (spec) => spec.id === action.remove.id,
                ),
              )
            }
            return next
          },
        },
        // Once a paste of HTML lands, its markers become placeholders, in the same undo step.
        appendTransaction: (transactions, _oldState, state) => {
          if (!pasted) return null
          const landed = transactions.some((tr) =>
            ['paste', 'drop'].includes(tr.getMeta('uiEvent')),
          )
          if (!landed) return null
          const { embedded } = pasted
          const problems: string[] = []
          const takenProblems = pasted.problems
          pasted = null

          // Only the text the paste changed: a marker typed into the field long ago is just text.
          const range = changedRange(transactions)
          const markers: { index: number; from: number; to: number }[] = []
          if (range)
            state.doc.nodesBetween(
              Math.max(0, range.from),
              Math.min(state.doc.content.size, range.to),
              (node, pos) => {
                if (!node.isText || !node.text) return
                for (const match of node.text.matchAll(MARKER)) {
                  const index = Number(match[1])
                  if (index >= embedded.length) continue
                  const from = pos + match.index
                  const to = from + match[0].length
                  // The pasted text can join a text node that was already there.
                  if (from < range.from || to > range.to) continue
                  markers.push({ index, from, to })
                }
              },
            )

          const tr = state.tr
          for (const { from, to } of [...markers].reverse()) tr.delete(from, to)

          const uploads: { id: object; file: File; alt: string }[] = []
          const placeholders: { id: object; pos: number }[] = []
          for (const { index, from } of markers) {
            const { file, alt } = embedded[index]
            const problem = imageUploadProblem(file)
            if (problem) {
              problems.push(problem)
              continue
            }
            const id = {}
            placeholders.push({ id, pos: tr.mapping.map(from) })
            uploads.push({ id, file, alt })
          }
          afterPaste = { uploads, problems: [...problems, ...takenProblems] }
          if (!tr.docChanged && placeholders.length === 0) return null
          return tr.setMeta(placeholderKey, {
            add: placeholders,
          } satisfies PlaceholderAction)
        },
        view: () => ({
          update: (view) => {
            if (!afterPaste) return
            const { uploads, problems } = afterPaste
            afterPaste = null
            storage.onError?.(joinProblems(problems))
            for (const { id, file, alt } of uploads)
              startUpload(view, id, file, alt)
          },
        }),
        props: {
          decorations: (state) => placeholderKey.getState(state),
          // Read for every paste or drop of HTML, before the editor parses it.
          transformPastedHTML: (html) => {
            pasted = null
            if (!storage.upload) return html
            const taken = takeImagesFromHtml(html)
            if (!taken) return html
            pasted = taken.images
            // A paste runs to the end in one go; one the length limit refuses never lands, and must
            // not be taken for the next paste.
            queueMicrotask(() => {
              pasted = null
            })
            return taken.html
          },
          transformPasted: (slice) =>
            storage.upload
              ? slice
              : new Slice(
                  withoutAttachmentImages(slice.content),
                  slice.openStart,
                  slice.openEnd,
                ),
          handlePaste: (view, event) => {
            const data = event.clipboardData
            const files = imageFilesIn(data)
            if (!storage.upload || files.length === 0) return false
            if (
              data?.types.includes('text/html') &&
              !holdsOnlyUnreachableImages(data.getData('text/html'))
            )
              return false
            event.preventDefault()
            // A paste replaces the selection, as any other paste does.
            if (!view.state.selection.empty)
              view.dispatch(view.state.tr.deleteSelection())
            uploadAll(view, files, view.state.selection.from)
            return true
          },
          handleDrop: (view, event, _slice, moved) => {
            if (moved || !storage.upload) return false
            const files = imageFilesIn(event.dataTransfer)
            if (files.length === 0) return false
            event.preventDefault()
            const pos =
              view.posAtCoords({ left: event.clientX, top: event.clientY })
                ?.pos ?? view.state.selection.from
            uploadAll(view, files, pos)
            return true
          },
        },
      }),
    ]
  },
})

/**
 * Turns image upload on for `editor` with `upload`, or off with `undefined`, and sets who to tell
 * when an image cannot be added.
 */
export const setMarkdownImageUpload = (
  editor: Editor,
  upload: ImageUploader | undefined,
  onError?: (message: string | null) => void,
): void => {
  editor.storage.markdownImageUpload.upload = upload
  editor.storage.markdownImageUpload.onError = onError
}
