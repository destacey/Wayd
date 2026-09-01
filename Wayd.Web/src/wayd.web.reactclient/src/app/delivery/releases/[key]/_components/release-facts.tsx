'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateOnly } from '@/src/components/common/wayd-grid'
import { ReleaseDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import Link from 'next/link'

export interface ReleaseFactsProps {
  release: ReleaseDto
}

/**
 * A release's stable facts, for the details panel.
 *
 * The three dates run in lifecycle order — planned for, cut, shipped — so the gaps between them are
 * readable at a glance. An absent date is shown rather than hidden: not yet cut is a fact about the
 * release, and omitting the row makes it look like the field does not exist.
 */
const ReleaseFacts = ({ release }: ReleaseFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Product">
        <Link href={`/product-management/products/${release.product.key}`}>
          {release.product.name}
        </Link>
      </LabeledContent>
      <LabeledContent label="Version">{release.version}</LabeledContent>
      {release.sequence != null && (
        <LabeledContent label="Sequence">{release.sequence}</LabeledContent>
      )}
    </Flex>

    <RecordFactsGroup label="Dates">
      <Flex vertical gap={10}>
        <LabeledContent label="Target">
          {formatDateOnly(release.targetDate) || 'Not set'}
        </LabeledContent>
        <LabeledContent label="Cut">
          {formatDateOnly(release.cutDate) ||
            // "Not yet" reads as still to come, which a shipped release will never do — cutting is
            // refused once it is released. Recording one is common with hand-entry and import.
            (release.releasedDate ? 'Not set' : 'Not yet cut')}
        </LabeledContent>
        <LabeledContent label="Released">
          {formatDateOnly(release.releasedDate) || 'Not yet released'}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>
  </>
)

export default ReleaseFacts
