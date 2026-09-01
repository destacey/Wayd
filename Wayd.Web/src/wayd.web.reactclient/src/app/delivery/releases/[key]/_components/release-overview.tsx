'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { WaydEmpty } from '@/src/components/common'
import { ReleaseDto } from '@/src/services/wayd-api'

export interface ReleaseOverviewProps {
  release: ReleaseDto
}

/**
 * A release's notes.
 *
 * The facts panel beside this already carries the dates, status and product, so repeating them here
 * would say everything twice. Deployments will land in this section once they have screens; until
 * then a release with no notes has nothing more to show than the panel does.
 */
const ReleaseOverview = ({ release }: ReleaseOverviewProps) =>
  release.notes ? (
    <MarkdownRenderer markdown={release.notes} />
  ) : (
    <WaydEmpty message="No notes have been recorded for this release." />
  )

export default ReleaseOverview
