'use client'

import { Image, Skeleton, Typography } from 'antd'
import { isAttachmentPath } from './attachments'
import { useImageSrc } from './use-image-src'

export interface MarkdownImageProps {
  src?: string
  alt?: string
  title?: string
}

/**
 * An image in rendered markdown. One uploaded into the field is loaded through the API, with a
 * placeholder holding its place meanwhile; one it cannot load says so in words, since a broken
 * image would not say whether it was deleted or is on a record the viewer cannot read.
 */
const MarkdownImage = ({ src, alt, title }: MarkdownImageProps) => {
  const state = useImageSrc(src)
  const label = alt || 'Image'

  if (state.status === 'loading')
    return (
      <span role="status" aria-label={`Loading ${label}`}>
        <Skeleton.Image active />
      </span>
    )

  if (state.status === 'failed' && isAttachmentPath(src))
    return (
      <Typography.Text type="secondary">[{label} unavailable]</Typography.Text>
    )

  return (
    <Image
      src={state.status === 'loaded' ? state.src : undefined}
      alt={label}
      title={title}
      fallback="/images/fallback-image.png"
    />
  )
}

export default MarkdownImage
