import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RoadmapActivityDetailsDto } from '@/src/services/wayd-api'
import { ColorPicker, Descriptions, Flex } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Item: DescriptionsItem } = Descriptions

interface RoadmapActivityDrawerItemProps {
  activity: RoadmapActivityDetailsDto
  openRoadmapItemDrawer: (itemId: string) => void
}

const RoadmapActivityDrawerItem: FC<RoadmapActivityDrawerItemProps> = (
  props: RoadmapActivityDrawerItemProps,
) => {
  const { activity } = props

  return (
    <Flex vertical gap="middle">
      <Descriptions column={1} size="small">
        <DescriptionsItem label="Name">{activity.name}</DescriptionsItem>
        <DescriptionsItem label="Type">{activity.type.name}</DescriptionsItem>
        <DescriptionsItem label="Parent">
          {activity.parent && (
            <a onClick={() => props.openRoadmapItemDrawer(activity.parent!.id)}>
              {activity.parent?.name}
            </a>
          )}
        </DescriptionsItem>
        <DescriptionsItem label="Color">
          {activity?.color && (
            <ColorPicker
              value={activity.color}
              size="small"
              showText
              disabled
            />
          )}
        </DescriptionsItem>
        <DescriptionsItem label="Start">
          {dayjs(activity.start).format('MMM D, YYYY')}
        </DescriptionsItem>
        <DescriptionsItem label="End">
          {dayjs(activity.end).format('MMM D, YYYY')}
        </DescriptionsItem>
      </Descriptions>
      <Descriptions column={1} layout="vertical" size="small">
        <DescriptionsItem label="Description">
          <MarkdownRenderer markdown={activity?.description} />
        </DescriptionsItem>
      </Descriptions>
    </Flex>
  )
}

export default RoadmapActivityDrawerItem
