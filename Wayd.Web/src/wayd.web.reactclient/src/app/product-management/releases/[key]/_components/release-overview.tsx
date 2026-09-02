'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { WaydEmpty } from '@/src/components/common'
import { ReleaseDto } from '@/src/services/wayd-api'

export interface ReleaseOverviewProps {
  release: ReleaseDto
}

/**
 * A release's notes, written for customers.
 *
 * The facts panel beside this already carries the dates, status and product, and the Contents section
 * lists what it announced, so repeating either here would say everything twice.
 */
const ReleaseOverview = ({ release }: ReleaseOverviewProps) =>
  release.notes ? (
    <MarkdownRenderer markdown={release.notes} />
  ) : (
    <WaydEmpty message="No notes have been recorded for this release." />
  )

export default ReleaseOverview
