'use client'

import { LabeledContent } from '@/src/components/common/content'
import { ProjectLifecycleDetailsDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface ProjectLifecycleFactsProps {
  lifecycle: ProjectLifecycleDetailsDto
}

/**
 * A project lifecycle's stable facts, for the details panel.
 *
 * These were the "Details" tab, which held two lines and opened by default —
 * so the stages, which are what a lifecycle actually is, sat one click behind
 * them. As facts they are visible alongside the stages instead.
 */
const ProjectLifecycleFacts = ({ lifecycle }: ProjectLifecycleFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Key">{lifecycle.key}</LabeledContent>
    <LabeledContent label="State">{lifecycle.state?.name}</LabeledContent>
    {lifecycle.description && (
      <LabeledContent label="Description">
        {lifecycle.description}
      </LabeledContent>
    )}
  </Flex>
)

export default ProjectLifecycleFacts
