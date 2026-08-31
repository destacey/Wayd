'use client'

import { LabeledContent } from '@/src/components/common/content'
import { StatusWorkflowDetailsDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface StatusWorkflowFactsProps {
  statusWorkflow: StatusWorkflowDetailsDto
}

/** A status workflow's stable facts, for the details panel. */
const StatusWorkflowFacts = ({ statusWorkflow }: StatusWorkflowFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Key">{statusWorkflow.key}</LabeledContent>
    <LabeledContent
      label="Owner"
      tooltip="The kind of record this workflow governs. It is fixed when the workflow is created."
    >
      {statusWorkflow.owner?.name}
    </LabeledContent>
    <LabeledContent label="State">{statusWorkflow.state}</LabeledContent>
    <LabeledContent
      label="System"
      tooltip="System workflows ship with the product and cannot be edited. Clone one to build your own."
    >
      {statusWorkflow.isSystem ? 'Yes' : 'No'}
    </LabeledContent>
    <LabeledContent
      label="Assigned"
      tooltip="Whether any records currently use this workflow."
    >
      {statusWorkflow.isAssigned ? 'Yes' : 'No'}
    </LabeledContent>
    {statusWorkflow.description && (
      <LabeledContent label="Description">
        {statusWorkflow.description}
      </LabeledContent>
    )}
  </Flex>
)

export default StatusWorkflowFacts
