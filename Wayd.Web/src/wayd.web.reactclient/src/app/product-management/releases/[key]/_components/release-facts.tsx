'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateOnly } from '@/src/components/common/wayd-grid'
import { ReleaseDto } from '@/src/services/wayd-api'
import { Flex, Typography } from 'antd'
import Link from 'next/link'

const { Text } = Typography

export interface ReleaseFactsProps {
  release: ReleaseDto
}

/**
 * A release's stable facts, for the details panel.
 *
 * Two dates rather than a version's three: a release is never cut, so there is nothing between being
 * planned and being announced. An absent date is shown rather than hidden — not yet announced is a
 * fact about the release, and omitting the row makes it look like the field does not exist.
 */
const ReleaseFacts = ({ release }: ReleaseFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Product">
        {release.product ? (
          <Link href={`/product-management/products/${release.product.key}`}>
            {release.product.name}
          </Link>
        ) : (
          // A release announcing work across product lines has no single owner. Saying so is more
          // useful than an empty row, which reads as missing data.
          <Text type="secondary">Spans product lines</Text>
        )}
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
        <LabeledContent label="Announced">
          {formatDateOnly(release.releasedDate) || 'Not yet announced'}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>

    <RecordFactsGroup label="Contents">
      <Flex vertical gap={10}>
        <LabeledContent label="Packages">
          {release.packages?.length ?? 0}
        </LabeledContent>
        <LabeledContent label="Carried directly">
          {release.versions?.length ?? 0}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>
  </>
)

export default ReleaseFacts
