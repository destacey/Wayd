'use client'

import { LabeledContent } from '@/src/components/common/content'
import { WorkProcessDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface WorkProcessFactsProps {
  workProcess: WorkProcessDto
}

/**
 * A work process's stable facts, for the details panel.
 *
 * One flat stack rather than `RecordFactsGroup`s: a work process has only what
 * it *is*, with nothing it links out to, and a group heading over the single
 * group would label the whole panel twice.
 *
 * No card of its own — the panel supplies the frame, and at mobile widths the
 * same stack renders inline.
 */
const WorkProcessFacts = ({ workProcess }: WorkProcessFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Key">{workProcess.key}</LabeledContent>
    <LabeledContent label="Ownership">
      {workProcess.ownership?.name}
    </LabeledContent>
    <LabeledContent label="Active">
      {workProcess.isActive ? 'Yes' : 'No'}
    </LabeledContent>
    {workProcess.description && (
      <LabeledContent label="Description">
        {workProcess.description}
      </LabeledContent>
    )}
  </Flex>
)

export default WorkProcessFacts
