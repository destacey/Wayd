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

/** Whether `src` refers to an image uploaded into a markdown field. */
export const isAttachmentPath = (src: string | null | undefined): boolean =>
  attachmentIdFrom(src) !== null
