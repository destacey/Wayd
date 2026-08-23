import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RoadmapMilestoneDetailsDto } from '@/src/services/wayd-api'
import { Descriptions, Flex } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Item: DescriptionsItem } = Descriptions

interface RoadmapMilestoneDrawerItemProps {
  milestone: RoadmapMilestoneDetailsDto
  openRoadmapItemDrawer: (itemId: string) => void
}

const RoadmapMilestoneDrawerItem: FC<RoadmapMilestoneDrawerItemProps> = (
  props: RoadmapMilestoneDrawerItemProps,
) => {
  const { milestone } = props

  return (
    <Flex vertical gap="middle">
      <Descriptions column={1} size="small">
        <DescriptionsItem label="Name">{milestone.name}</DescriptionsItem>
        <DescriptionsItem label="Type">{milestone.type.name}</DescriptionsItem>
        <DescriptionsItem label="Parent">
          {milestone.parent && (
            <a
              onClick={() => props.openRoadmapItemDrawer(milestone.parent!.id)}
            >
              {milestone.parent?.name}
            </a>
          )}
        </DescriptionsItem>
        <DescriptionsItem label="Date">
          {dayjs(milestone.date).format('MMM D, YYYY')}
        </DescriptionsItem>
      </Descriptions>
      <Descriptions column={1} layout="vertical" size="small">
        <DescriptionsItem label="Description">
          <MarkdownRenderer markdown={milestone?.description} />
        </DescriptionsItem>
      </Descriptions>
    </Flex>
  )
}

export default RoadmapMilestoneDrawerItem
