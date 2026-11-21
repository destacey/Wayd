import { ExportOutlined } from '@ant-design/icons'
import Link from 'next/link'
import { ReactNode } from 'react'
import { safeLinkHref } from './markdown/wysiwyg/safe-urls'

export interface ExternalIconLinkProps {
  content: string | ReactNode
  url?: string | null
  tooltip?: string
}

const ExternalIconLink = ({
  content,
  url,
  tooltip,
}: ExternalIconLinkProps): ReactNode => {
  const href = safeLinkHref(url)
  if (!href) return content

  return (
    <>
      {content}
      &nbsp;
      {
        <Link
          href={href}
          target="_blank"
          rel="noopener noreferrer"
          title={tooltip}
        >
          <ExportOutlined style={{ width: '12px' }} />
        </Link>
      }
    </>
  )
}

export default ExternalIconLink
