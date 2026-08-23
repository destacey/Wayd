import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RoadmapTimeboxDetailsDto } from '@/src/services/wayd-api'
import { Descriptions, Flex } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Item: DescriptionsItem } = Descriptions

interface RoadmapTimeboxDrawerItemProps {
  timebox: RoadmapTimeboxDetailsDto
  openRoadmapItemDrawer: (itemId: string) => void
}

const RoadmapTimeboxDrawerItem: FC<RoadmapTimeboxDrawerItemProps> = (
  props: RoadmapTimeboxDrawerItemProps,
) => {
  const { timebox } = props

  return (
    <Flex vertical gap="middle">
      <Descriptions column={1} size="small">
        <DescriptionsItem label="Name">{timebox.name}</DescriptionsItem>
        <DescriptionsItem label="Type">{timebox.type.name}</DescriptionsItem>
        <DescriptionsItem label="Parent">
          {timebox.parent && (
            <a onClick={() => props.openRoadmapItemDrawer(timebox.parent!.id)}>
              {timebox.parent?.name}
            </a>
          )}
        </DescriptionsItem>
        <DescriptionsItem label="Start">
          {dayjs(timebox.start).format('MMM D, YYYY')}
        </DescriptionsItem>
        <DescriptionsItem label="End">
          {dayjs(timebox.end).format('MMM D, YYYY')}
        </DescriptionsItem>
      </Descriptions>
      <Descriptions column={1} layout="vertical" size="small">
        <DescriptionsItem label="Description">
          <MarkdownRenderer markdown={timebox?.description} />
        </DescriptionsItem>
      </Descriptions>
    </Flex>
  )
}

export default RoadmapTimeboxDrawerItem
