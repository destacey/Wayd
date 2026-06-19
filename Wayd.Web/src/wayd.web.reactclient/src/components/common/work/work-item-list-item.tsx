import { WorkItemListDto } from '@/src/services/wayd-api'
import { Flex, Space, Tag, Typography } from 'antd'
import Link from 'next/link'
import ExternalIconLink from '../external-icon-link'
import WaydList from '../wayd-list'
import { getWorkStatusCategoryColor } from '@/src/utils'
import SprintLink from '../planning/sprint-link'
import { WorkTypeTier } from '../../types'

const { Item } = WaydList
const { Meta } = Item
const { Text } = Typography

export interface WorkItemListItemProps {
  workItem: WorkItemListDto
}

const WorkItemListItem = ({ workItem }: WorkItemListItemProps) => {
  const workItemTitle = (
    <Link
      href={`/work/workspaces/${workItem.workspace.key}/work-items/${workItem.key}`}
    >
      {workItem.key} - {workItem.title}&nbsp;
    </Link>
  )
  return (
    <Item key={workItem.id}>
      <Meta
        title={
          <ExternalIconLink
            content={workItemTitle}
            url={workItem.externalViewWorkItemUrl}
            tooltip="Open in external system"
          />
        }
        description={<WorkItemListItemDescription workItem={workItem} />}
      />
    </Item>
  )
}

export default WorkItemListItem

const WorkItemListItemDescription = ({ workItem }: WorkItemListItemProps) => {
  const isRequirement = workItem.type.tier.id === WorkTypeTier.Requirement
  const showSprint = isRequirement && workItem.sprint

  return (
    <Flex vertical gap="small">
      {showSprint && (
        <Text type="secondary">
          Sprint: <SprintLink sprint={workItem.sprint} />
        </Text>
      )}
      <Space wrap>
        <Tag title="Work Item Type">{workItem.type.name}</Tag>
        <Tag
          title="Work Item Status"
          color={getWorkStatusCategoryColor(workItem.statusCategory.name)}
        >
          {workItem.status}
        </Tag>
        {workItem.team && (
          <Tag title="Work Item Team">{workItem.team.name}</Tag>
        )}
      </Space>
    </Flex>
  )
}
