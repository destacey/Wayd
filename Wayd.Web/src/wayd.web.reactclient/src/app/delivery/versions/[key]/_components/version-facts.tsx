'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateOnly } from '@/src/components/common/wayd-grid'
import { VersionDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import Link from 'next/link'

export interface VersionFactsProps {
  version: VersionDto
}

/**
 * A version's stable facts, for the details panel.
 *
 * The three dates run in lifecycle order — planned for, cut, shipped — so the gaps between them are
 * readable at a glance. An absent date is shown rather than hidden: not yet cut is a fact about the
 * version, and omitting the row makes it look like the field does not exist.
 */
const VersionFacts = ({ version }: VersionFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Product">
        <Link href={`/product-management/products/${version.product.key}`}>
          {version.product.name}
        </Link>
      </LabeledContent>
      <LabeledContent label="Version">{version.number}</LabeledContent>
      {version.sequence != null && (
        <LabeledContent label="Sequence">{version.sequence}</LabeledContent>
      )}
    </Flex>

    <RecordFactsGroup label="Dates">
      <Flex vertical gap={10}>
        <LabeledContent label="Target">
          {formatDateOnly(version.targetDate) || 'Not set'}
        </LabeledContent>
        <LabeledContent label="Cut">
          {formatDateOnly(version.cutDate) ||
            // "Not yet" reads as still to come, which a shipped version will never do — cutting is
            // refused once it is released. Recording one is common with hand-entry and import.
            (version.releasedDate ? 'Not set' : 'Not yet cut')}
        </LabeledContent>
        <LabeledContent label="Released">
          {formatDateOnly(version.releasedDate) || 'Not yet released'}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>
  </>
)

export default VersionFacts
