import {
  attachmentsGetContent,
  attachmentsUpload,
} from '@/src/services/api/attachments'
import type { AttachmentDto } from '@/src/services/api/wayd.schemas'

import { attachmentPathFor } from './attachment-path'

export {
  ATTACHMENT_SRC_ATTRIBUTE,
  attachmentIdFrom,
  attachmentPathFor,
  isAttachmentPath,
} from './attachment-path'

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

/** The attachment id each object URL in `loaded` shows, so a copy of a shown image can be traced back. */
const idsByObjectUrl = new Map<string, string>()

const objectUrlFor = (blob: Blob, id: string): string => {
  const url = URL.createObjectURL(blob)
  idsByObjectUrl.set(url, id)
  return url
}

/**
 * The attachment path an object URL from `loadAttachmentUrl` or `uploadImage` shows, or `null` for any
 * other address. Copying an image Wayd is showing copies that object URL rather than its path.
 */
export const attachmentPathForObjectUrl = (src: string): string | null => {
  const id = idsByObjectUrl.get(src.trim())
  return id ? attachmentPathFor(id) : null
}

/**
 * A URL the browser can show the attachment from. The API needs the session's token, which an
 * `<img>` cannot send, so the image is fetched through the API client and shown from memory.
 */
export const loadAttachmentUrl = (id: string): Promise<string> => {
  const key = id.toLowerCase()
  const existing = loaded.get(key)
  if (existing) return existing

  const url = attachmentsGetContent(key).then((blob) => objectUrlFor(blob, key))
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
  const key = attachment.id.toLowerCase()
  loaded.set(key, Promise.resolve(objectUrlFor(file, key)))
  return attachment
}

/** The image files in a paste or drop, in order; empty when it holds none. */
export const imageFilesIn = (data: DataTransfer | null): File[] =>
  Array.from(data?.files ?? []).filter((file) => file.type.startsWith('image/'))

/** A `data:` URL: its media type, its `;` parameters (`;base64` among them) and its payload. */
const DATA_URL = /^data:([^;,]*)((?:;[^;,]*)*),(.*)$/s

/** A percent-encoded payload's bytes: each `%XX` is one byte, anything else is its UTF-8. */
const percentDecodedBytes = (payload: string): Uint8Array<ArrayBuffer> => {
  const bytes: number[] = []
  const encoder = new TextEncoder()
  for (let i = 0; i < payload.length; i++) {
    const hex = payload.slice(i + 1, i + 3)
    if (payload[i] === '%' && /^[0-9a-f]{2}$/i.test(hex)) {
      bytes.push(parseInt(hex, 16))
      i += 2
    } else {
      bytes.push(...encoder.encode(payload[i]))
    }
  }
  return Uint8Array.from(bytes)
}

/**
 * Roughly how many bytes the `data:` URL `src` holds, without decoding it, so an image far over the
 * limit is refused before a paste spends time decoding it.
 */
export const dataUrlSize = (src: string): number => {
  const comma = src.indexOf(',')
  const payload = src.length - comma - 1
  return /;base64,/i.test(src.slice(0, comma + 1))
    ? Math.floor((payload * 3) / 4)
    : payload
}

/**
 * The image a `data:` URL holds, as a file named `name`, or `null` when `src` is not a `data:` URL
 * of an image. Decoded here rather than fetched, since the content security policy blocks fetching
 * a `data:` URL. Whether the API accepts the image is `imageUploadProblem`'s to say.
 */
export const imageFileFromDataUrl = (
  src: string,
  name: string,
): File | null => {
  const match = DATA_URL.exec(src.trim())
  if (!match) return null
  const type = match[1].toLowerCase()
  if (!type.startsWith('image/')) return null

  try {
    const isBase64 = match[2].toLowerCase().split(';').includes('base64')
    const bytes = isBase64
      ? Uint8Array.from(atob(match[3].replace(/\s/g, '')), (char) =>
          char.charCodeAt(0),
        )
      : percentDecodedBytes(match[3])
    const extension = type.slice('image/'.length).split('+')[0] || 'img'
    return new File([bytes], `${name}.${extension}`, { type })
  } catch {
    return null
  }
}
