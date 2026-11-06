'use client'

import { LabeledContent } from '@/src/components/common/content'
import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface TagCategoryFactsProps {
  category: IntakeRequestTagCategoryDto
}

/**
 * A tag category's stable facts, for the details panel.
 *
 * "Allows Many" is here rather than on the edit form because it is fixed once
 * set — narrowing it later would leave requests holding more tags than the category
 * permits — so it is something to read, not something to change.
 */
const TagCategoryFacts = ({ category }: TagCategoryFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Active">
      {category.isActive ? 'Yes' : 'No'}
    </LabeledContent>
    <LabeledContent label="Allows Many">
      {category.allowsMany ? 'Yes' : 'No'}
    </LabeledContent>
    {category.isSystem && (
      <LabeledContent label="System">
        Platform-defined. Its name and tags are read-only.
      </LabeledContent>
    )}
    {category.description && (
      <LabeledContent label="Description">
        {category.description}
      </LabeledContent>
    )}
  </Flex>
)

export default TagCategoryFacts
