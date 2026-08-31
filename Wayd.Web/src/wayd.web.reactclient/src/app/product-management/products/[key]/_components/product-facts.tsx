'use client'

import { LabeledContent } from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import { ProductDto } from '@/src/services/wayd-api'
import { Flex, Tag } from 'antd'
import Link from 'next/link'

export interface ProductFactsProps {
  product: ProductDto
}

/**
 * A product's stable facts, for the details panel.
 *
 * What it is and what it is part of, then the labels it carries. Releasability
 * sits beside the type because it is the type's consequence and the thing that
 * decides whether the Releases section means anything for this node.
 */
const ProductFacts = ({ product }: ProductFactsProps) => {
  const tagsByCategory = (product.tags ?? []).reduce<
    Record<string, { categoryName: string; tagNames: string[] }>
  >((acc, tag) => {
    const group = (acc[tag.categoryId] ??= {
      categoryName: tag.categoryName,
      tagNames: [],
    })
    group.tagNames.push(tag.tagName)
    return acc
  }, {})

  const categories = Object.values(tagsByCategory).sort((a, b) =>
    a.categoryName.localeCompare(b.categoryName, undefined, {
      sensitivity: 'base',
    }),
  )

  return (
    <>
      <Flex vertical gap={10}>
        {product.description && (
          <LabeledContent label="Description">
            <MarkdownRenderer markdown={product.description} />
          </LabeledContent>
        )}
        <LabeledContent label="Type">{product.type?.name}</LabeledContent>
        <LabeledContent label="Releasable">
          {product.isReleasable ? 'Yes' : 'No'}
        </LabeledContent>
        {product.externalId && (
          <LabeledContent label="External Id">
            {product.externalId}
          </LabeledContent>
        )}
      </Flex>

      {product.parent && (
        <RecordFactsGroup label="Relationships">
          <LabeledContent label="Parent">
            <Link href={`/product-management/products/${product.parent.key}`}>
              {product.parent.name}
            </Link>
          </LabeledContent>
        </RecordFactsGroup>
      )}

      {categories.length > 0 && (
        <RecordFactsGroup label="Tags">
          <Flex vertical gap={10}>
            {categories.map((category) => (
              <LabeledContent
                key={category.categoryName}
                label={category.categoryName}
              >
                <Flex wrap gap={4}>
                  {category.tagNames.sort().map((name) => (
                    <Tag key={name}>{name}</Tag>
                  ))}
                </Flex>
              </LabeledContent>
            ))}
          </Flex>
        </RecordFactsGroup>
      )}
    </>
  )
}

export default ProductFacts
