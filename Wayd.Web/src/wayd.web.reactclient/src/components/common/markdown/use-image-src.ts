'use client'

import { useEffect, useState } from 'react'
import { attachmentIdFrom, loadAttachmentUrl } from './attachments'

/** What an image in markdown can be shown from, as it is resolved. */
export type ImageSrcState =
  | { status: 'loading' }
  | { status: 'loaded'; src: string }
  | { status: 'failed' }

/**
 * Where to show an image in markdown from: an attachment is loaded through the API, any other
 * address is used as it is. An attachment that cannot be loaded — deleted, or on a record the
 * viewer cannot read — fails rather than showing a broken image.
 */
export const useImageSrc = (src: string | null | undefined): ImageSrcState => {
  const id = attachmentIdFrom(src)
  const [resolved, setResolved] = useState<{
    id: string
    state: ImageSrcState
  } | null>(null)

  useEffect(() => {
    if (!id) return
    let current = true
    loadAttachmentUrl(id).then(
      (url) =>
        current && setResolved({ id, state: { status: 'loaded', src: url } }),
      () => current && setResolved({ id, state: { status: 'failed' } }),
    )
    return () => {
      current = false
    }
  }, [id])

  if (!id) return src ? { status: 'loaded', src } : { status: 'failed' }
  return resolved?.id === id ? resolved.state : { status: 'loading' }
}
