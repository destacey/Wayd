import { ForecastWorkItemDto } from '@/src/services/wayd-api'
import { Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'

const { Text } = Typography

export interface ForecastWorkItemLinkProps {
  workItem: ForecastWorkItemDto
  showTitle?: boolean
}

const ForecastWorkItemLink: FC<ForecastWorkItemLinkProps> = ({
  workItem,
  showTitle = true,
}) => (
  <span>
    <Link
      href={`/work/workspaces/${workItem.workspaceKey}/work-items/${workItem.key}`}
    >
      {workItem.key}
    </Link>
    {showTitle && <Text type="secondary"> {workItem.title}</Text>}
  </span>
)

export default ForecastWorkItemLink
