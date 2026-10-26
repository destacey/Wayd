'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { IntakeDeskDto, IntakeDeskVisibilityDto } from '@/src/services/wayd-api'
import { FC } from 'react'
import { describeWaiting } from '../../../_components/desk-status'

export interface IntakeDeskFactsProps {
  desk: IntakeDeskDto
  visibilities: IntakeDeskVisibilityDto[] | undefined
}

/**
 * The desk's stable facts. What is waiting appears only when the server sent it: to the desk's members and
 * the Demand Management Admin.
 */
const IntakeDeskFacts: FC<IntakeDeskFactsProps> = ({ desk, visibilities }) => (
  <RecordFactsGroup label="Desk">
    <LabeledContent label="What to Bring Here">
      {desk.description}
    </LabeledContent>
    <LabeledContent label="Visibility">
      {visibilities?.find((v) => v.code === desk.visibility)?.name ??
        desk.visibility}
    </LabeledContent>
    <LabeledContent label="Open to New Requests">
      {desk.isOpen ? 'Yes' : 'No'}
    </LabeledContent>
    {desk.waiting && (
      <LabeledContent label="Waiting">
        {describeWaiting(desk.waiting)}
      </LabeledContent>
    )}
  </RecordFactsGroup>
)

export default IntakeDeskFacts
