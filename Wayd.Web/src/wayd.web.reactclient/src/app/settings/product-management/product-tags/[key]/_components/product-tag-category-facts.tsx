'use client'

import { LabeledContent } from '@/src/components/common/content'
import { ProductTagCategoryDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface ProductTagCategoryFactsProps {
  category: ProductTagCategoryDto
}

/**
 * A tag axis's stable facts, for the details panel.
 *
 * "Allows Many" is here rather than on the edit form because it is fixed once
 * set — narrowing it later would leave products holding more tags than the axis
 * permits — so it is something to read, not something to change.
 */
const ProductTagCategoryFacts = ({ category }: ProductTagCategoryFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Key">{category.key}</LabeledContent>
    <LabeledContent label="Active">
      {category.isActive ? 'Yes' : 'No'}
    </LabeledContent>
    <LabeledContent label="Allows Many">
      {category.allowsMany ? 'Yes' : 'No'}
    </LabeledContent>
    {category.isSystem && (
      <LabeledContent label="System">
        Platform-seeded. Its name and tags are read-only.
      </LabeledContent>
    )}
    {category.description && (
      <LabeledContent label="Description">{category.description}</LabeledContent>
    )}
  </Flex>
)

export default ProductTagCategoryFacts
