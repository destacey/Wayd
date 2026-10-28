'use client'

import { LabeledContent } from '@/src/components/common/content'
import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface TagAxisFactsProps {
  axis: IntakeRequestTagCategoryDto
}

/**
 * A tag axis's stable facts, for the details panel.
 *
 * "Allows Many" is here rather than on the edit form because it is fixed once
 * set — narrowing it later would leave requests holding more tags than the axis
 * permits — so it is something to read, not something to change.
 */
const TagAxisFacts = ({ axis }: TagAxisFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Active">
      {axis.isActive ? 'Yes' : 'No'}
    </LabeledContent>
    <LabeledContent label="Allows Many">
      {axis.allowsMany ? 'Yes' : 'No'}
    </LabeledContent>
    {axis.isSystem && (
      <LabeledContent label="System">
        Platform-defined. Its name and tags are read-only.
      </LabeledContent>
    )}
    {axis.description && (
      <LabeledContent label="Description">{axis.description}</LabeledContent>
    )}
  </Flex>
)

export default TagAxisFacts
