import { StatusCategory } from '@/src/services/wayd-api'

/**
 * What a status category means, for a reader who does not know the workflow.
 *
 * A status name is the workflow's own word and an administrator can rename it to anything; the
 * category is the fixed meaning behind it, and what rollups and filters group on. Without it,
 * "Shipped" and "Shelved" are equally opaque on a workflow you have not seen before.
 */
export const statusCategoryDescription = (category: StatusCategory): string => {
  switch (category) {
    case StatusCategory.Proposed:
      return 'Proposed — not started yet.'
    case StatusCategory.Active:
      return 'Active — in progress.'
    case StatusCategory.Done:
      return 'Done — completed successfully.'
    case StatusCategory.Removed:
      return 'Removed — abandoned or withdrawn, not completed.'
    default:
      return ''
  }
}
