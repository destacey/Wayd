import {
  attachmentsGetContent,
  attachmentsUpload,
} from '@/src/services/api/attachments'
import type { AttachmentDto } from '@/src/services/api/wayd.schemas'

export { attachmentIdFrom, isAttachmentPath } from './attachment-path'

/** The media types the API accepts for upload; anything else is refused before it is sent. */
export const UPLOADABLE_IMAGE_TYPES = [
  'image/png',
  'image/jpeg',
  'image/gif',
  'image/webp',
] as const

/** The largest image the API accepts, in bytes. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024

/**
 * Object URLs of the attachments loaded this session, by id. An attachment's bytes never change,
 * so each is fetched once and kept until the page unloads; a failed load is forgotten, so the next
 * image that mounts with it tries again.
 */
const loaded = new Map<string, Promise<string>>()

/**
 * A URL the browser can show the attachment from. The API needs the session's token, which an
 * `<img>` cannot send, so the image is fetched through the API client and shown from memory.
 */
export const loadAttachmentUrl = (id: string): Promise<string> => {
  const key = id.toLowerCase()
  const existing = loaded.get(key)
  if (existing) return existing

  const url = attachmentsGetContent(key).then((blob) =>
    URL.createObjectURL(blob),
  )
  loaded.set(key, url)
  url.catch(() => loaded.delete(key))
  return url
}

/** Why `file` cannot be uploaded, or `null` when it can be sent. The API checks again. */
export const imageUploadProblem = (file: File): string | null => {
  if (!(UPLOADABLE_IMAGE_TYPES as readonly string[]).includes(file.type))
    return 'Only PNG, JPEG, GIF and WebP images can be added.'
  if (file.size > MAX_IMAGE_BYTES) return 'Images can be at most 5 MB.'
  return null
}

/**
 * Uploads an image, which only the uploader can see until the record referring to it is saved. The
 * file itself is shown for it meanwhile, so the image appears without being downloaded again.
 */
export const uploadImage = async (file: File): Promise<AttachmentDto> => {
  const attachment = await attachmentsUpload({ file })
  loaded.set(
    attachment.id.toLowerCase(),
    Promise.resolve(URL.createObjectURL(file)),
  )
  return attachment
}

/** The image files in a paste or drop, in order; empty when it holds none. */
export const imageFilesIn = (data: DataTransfer | null): File[] =>
  Array.from(data?.files ?? []).filter((file) => file.type.startsWith('image/'))
