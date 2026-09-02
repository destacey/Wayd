'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { WaydEmpty } from '@/src/components/common'
import { VersionDto } from '@/src/services/wayd-api'

export interface VersionOverviewProps {
  version: VersionDto
}

/**
 * A version's notes.
 *
 * The facts panel beside this already carries the dates, status and product, so repeating them here
 * would say everything twice. Deployments will land in this section once they have screens; until
 * then a version with no notes has nothing more to show than the panel does.
 */
const VersionOverview = ({ version }: VersionOverviewProps) =>
  version.notes ? (
    <MarkdownRenderer markdown={version.notes} />
  ) : (
    <WaydEmpty message="No notes have been recorded for this version." />
  )

export default VersionOverview
