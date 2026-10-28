'use client'

import { LabeledContent } from '@/src/components/common/content'
import {
  IntakeDeskDto,
  IntakeDeskVisibility,
  IntakeDeskVisibilityDto,
} from '@/src/services/wayd-api'
import {
  useGetIntakeChannelsQuery,
  useGetIntakeRequestTagCategoriesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Flex, Skeleton, Typography } from 'antd'
import { FC } from 'react'
import IntakeDeskWorkflow from './intake-desk-workflow'

const { Text } = Typography

export interface IntakeDeskSettingsProps {
  desk: IntakeDeskDto
  visibilities: IntakeDeskVisibilityDto[] | undefined
}

const yesNo = (value: boolean) => (value ? 'Yes' : 'No')

/**
 * The desk's settings, each saying plainly what it does, so nobody has to infer what a visibility exposes.
 */
const IntakeDeskSettings: FC<IntakeDeskSettingsProps> = ({
  desk,
  visibilities,
}) => {
  const visibility = visibilities?.find((v) => v.code === desk.visibility)
  const { data: channels } = useGetIntakeChannelsQuery({
    includeArchived: true,
  })
  // An archived channel stays chosen but is offered by no desk.
  const offered = (channels ?? []).filter(
    (c) => c.isActive && desk.channelIds.includes(c.id),
  )
  const { data: tagCategories } = useGetIntakeRequestTagCategoriesQuery({
    includeInactive: true,
  })
  // An inactive axis stays chosen but is offered by no desk.
  const offeredAxes = (tagCategories ?? []).filter(
    (c) => c.isActive && desk.tagCategoryIds.includes(c.id),
  )

  return (
    <Flex vertical gap={16} style={{ maxWidth: 720 }}>
      <LabeledContent label="Name">{desk.name}</LabeledContent>
      <LabeledContent label="What to Bring Here">
        {desk.description}
      </LabeledContent>
      <LabeledContent label="Review Workflow">
        <IntakeDeskWorkflow deskId={desk.id} deskName={desk.name} />
      </LabeledContent>
      <LabeledContent label="Who Can See Its Requests">
        <Flex vertical>
          <Text>{visibility?.name ?? desk.visibility}</Text>
          {visibility?.description && (
            <Text type="secondary">{visibility.description}</Text>
          )}
        </Flex>
      </LabeledContent>
      <LabeledContent label="Channels Offered">
        {desk.offersAllChannels ? (
          'Every channel, including ones added later'
        ) : !channels ? (
          // Saying "None" before the names arrive would be wrong for a desk that chose some.
          <Skeleton.Input active size="small" />
        ) : offered.length > 0 ? (
          offered.map((c) => c.name).join(', ')
        ) : (
          <Flex vertical>
            <Text>None</Text>
            <Text type="secondary">
              Requesters are not asked where a request came from.
            </Text>
          </Flex>
        )}
      </LabeledContent>
      <LabeledContent label="Tag Axes Offered">
        {desk.offersAllTagCategories ? (
          'Every tag axis, including ones added later'
        ) : !tagCategories ? (
          <Skeleton.Input active size="small" />
        ) : offeredAxes.length > 0 ? (
          offeredAxes.map((c) => c.name).join(', ')
        ) : (
          <Flex vertical>
            <Text>None</Text>
            <Text type="secondary">The desk's requests are not tagged.</Text>
          </Flex>
        )}
      </LabeledContent>
      <LabeledContent label="Open to New Requests">
        {yesNo(desk.isOpen)}
      </LabeledContent>
      <LabeledContent label="Requesters Can Choose It">
        {yesNo(desk.isRequesterSelectable)}
      </LabeledContent>
      {desk.visibility === IntakeDeskVisibility.Private && (
        <LabeledContent label="Requesters May Add Participants">
          {yesNo(desk.requesterMayAddParticipants)}
        </LabeledContent>
      )}
    </Flex>
  )
}

export default IntakeDeskSettings
