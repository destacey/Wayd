/**
 * An image uploaded into a markdown field, as its markdown refers to it: `/api/attachments/{id}`,
 * a path on the API rather than on this site.
 */
const ATTACHMENT_PATH =
  /^\/api\/attachments\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i

/** The attachment id in `src` when it is an attachment path, otherwise `null`. */
export const attachmentIdFrom = (
  src: string | null | undefined,
): string | null => ATTACHMENT_PATH.exec(src?.trim() ?? '')?.[1] ?? null

/** The path markdown refers to the attachment `id` by. */
export const attachmentPathFor = (id: string): string =>
  `/api/attachments/${id.toLowerCase()}`

/** Whether `src` refers to an image uploaded into a markdown field. */
export const isAttachmentPath = (src: string | null | undefined): boolean =>
  attachmentIdFrom(src) !== null

/**
 * Where an uploaded image's address is written when the image is rendered as HTML. Kept out of
 * `src`, where the browser would ask this site for it.
 */
export const ATTACHMENT_SRC_ATTRIBUTE = 'data-attachment-src'
