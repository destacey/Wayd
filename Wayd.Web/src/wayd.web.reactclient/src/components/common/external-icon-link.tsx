import { ExportOutlined } from '@ant-design/icons'
import Link from 'next/link'
import { ReactNode } from 'react'

export interface ExternalIconLinkProps {
  content: string | ReactNode
  url?: string
  tooltip?: string
}

const ExternalIconLink = ({
  content,
  url,
  tooltip,
}: ExternalIconLinkProps): ReactNode => {
  if (!url) return content

  return (
    <>
      {content}
      &nbsp;
      {
        <Link
          href={url}
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
