import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'

/**
 * What an action or its dialog needs to know about a tag axis.
 *
 * Narrower than the DTO so the grid row and the record page build their `⋯`
 * from the same call. `isSystem` and `tags` decide which actions exist at all:
 * a platform-defined axis can be deactivated but never edited or deleted, and
 * an axis whose tags requests carry cannot be deleted.
 */
export type TagAxisActionTarget = Pick<
  IntakeRequestTagCategoryDto,
  'id' | 'name' | 'description' | 'isActive' | 'isSystem' | 'tags'
>

/** How many requests carry a tag from the axis — what deleting it would orphan. */
export const axisRequestCount = (axis: Pick<TagAxisActionTarget, 'tags'>) =>
  (axis.tags ?? []).reduce((total, tag) => total + tag.requestCount, 0)
