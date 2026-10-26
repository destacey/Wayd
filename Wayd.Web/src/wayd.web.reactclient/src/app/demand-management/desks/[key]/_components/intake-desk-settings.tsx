'use client'

import { LabeledContent } from '@/src/components/common/content'
import {
  IntakeDeskDto,
  IntakeDeskVisibility,
  IntakeDeskVisibilityDto,
} from '@/src/services/wayd-api'
import { Flex, Typography } from 'antd'
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
