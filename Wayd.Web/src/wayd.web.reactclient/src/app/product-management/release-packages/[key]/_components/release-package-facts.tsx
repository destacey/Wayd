'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateOnly } from '@/src/components/common/wayd-grid'
import { ManifestEntryKind, ReleasePackageDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface ReleasePackageFactsProps {
  releasePackage: ReleasePackageDto
}

/**
 * A package's stable facts, for the details panel.
 *
 * The manifest breakdown is counted here rather than read from the DTO, which carries no totals. The
 * two kinds are shown separately because they answer different questions — what changed in this
 * package, and what merely travelled with it.
 */
const ReleasePackageFacts = ({ releasePackage }: ReleasePackageFactsProps) => {
  const components = releasePackage.components ?? []
  const changedCount = components.filter(
    (component) => component.kind === ManifestEntryKind.Changed,
  ).length
  const carriedForwardCount = components.length - changedCount

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Version">{releasePackage.version}</LabeledContent>
      </Flex>

      <RecordFactsGroup label="Manifest">
        <Flex vertical gap={10}>
          <LabeledContent label="Components">{components.length}</LabeledContent>
          <LabeledContent label="Changed">{changedCount}</LabeledContent>
          <LabeledContent label="Carried Forward">
            {carriedForwardCount}
          </LabeledContent>
        </Flex>
      </RecordFactsGroup>

      <RecordFactsGroup label="Dates">
        <Flex vertical gap={10}>
          <LabeledContent label="Target">
            {formatDateOnly(releasePackage.targetDate) || 'Not set'}
          </LabeledContent>
          <LabeledContent label="Released">
            {formatDateOnly(releasePackage.releasedDate) || 'Not yet released'}
          </LabeledContent>
        </Flex>
      </RecordFactsGroup>
    </>
  )
}

export default ReleasePackageFacts
