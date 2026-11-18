import { type Editor, Extension } from '@tiptap/core'
import { Fragment, type Node as ProseMirrorNode, Slice } from '@tiptap/pm/model'
import { Plugin, PluginKey, type EditorState } from '@tiptap/pm/state'
import { Decoration, DecorationSet, type EditorView } from '@tiptap/pm/view'
import { errorDetail } from '@/src/utils/problem-details'
import {
  imageFilesIn,
  imageUploadProblem,
  isAttachmentPath,
} from '../attachments'

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
  { add: { id: object; pos: number } } | { remove: { id: object } }

const placeholderKey = new PluginKey<DecorationSet>('markdownImageUpload')

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
 * A paste that carries HTML too is left alone: copying a table from a spreadsheet or an image from a
 * web page puts a picture of it on the clipboard beside the content the user meant to copy.
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

    const uploadAll = (view: EditorView, files: File[], pos: number) => {
      const upload = storage.upload
      if (!upload) return
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
            add: { id, pos },
          } satisfies PlaceholderAction),
        )

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
                  view.state.schema.nodes.image.create({ src: path, alt: '' }),
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
              next = next.add(tr.doc, [
                Decoration.widget(action.add.pos, placeholderWidget, {
                  id: action.add.id,
                }),
              ])
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
        props: {
          decorations: (state) => placeholderKey.getState(state),
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
            if (data?.types.includes('text/html')) return false
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
