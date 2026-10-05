import { SizingMethod } from '@/src/services/wayd-api'
import type { ColumnDef } from '../wayd-grid-core'

/** A work item's three estimates, each its own field and null when unestimated. */
export interface WorkItemEstimates {
  storyPoints?: number | undefined
  effort?: number | undefined
  size?: number | undefined
}

/**
 * The estimate a work item grid shows by default: the team's sizing method, or Story Points when there is no
 * estimate to follow — a team that sizes by count, or a grid of several teams' work.
 */
export const defaultEstimate = (
  sizingMethod?: SizingMethod | null,
): SizingMethod =>
  sizingMethod === SizingMethod.Effort || sizingMethod === SizingMethod.Size
    ? sizingMethod
    : SizingMethod.StoryPoints

/**
 * Story Points, Effort and Size columns, showing the one `sizingMethod` names and starting the others hidden
 * but available in Choose Columns. The default sits below the user's own column choices, so it never
 * overrides a column the user showed or hid.
 */
export const estimateColumns = <T extends WorkItemEstimates>(
  sizingMethod?: SizingMethod | null,
  size = 80,
): ColumnDef<T, any>[] => {
  const shown = defaultEstimate(sizingMethod)
  return [
    {
      id: 'storyPoints',
      accessorFn: (row) => row.storyPoints,
      header: 'SPs',
      size,
      meta: {
        headerTooltip: 'Story Points',
        exportHeader: 'Story Points',
        hiddenByDefault: shown !== SizingMethod.StoryPoints,
      },
    },
    {
      id: 'effort',
      accessorFn: (row) => row.effort,
      header: 'Effort',
      size,
      meta: { hiddenByDefault: shown !== SizingMethod.Effort },
    },
    {
      id: 'size',
      accessorFn: (row) => row.size,
      header: 'Size',
      size,
      meta: { hiddenByDefault: shown !== SizingMethod.Size },
    },
  ]
}
