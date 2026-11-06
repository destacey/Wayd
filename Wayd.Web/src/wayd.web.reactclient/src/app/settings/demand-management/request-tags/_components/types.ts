import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'

/**
 * What an action or its dialog needs to know about a tag category.
 *
 * Narrower than the DTO so the grid row and the record page build their `⋯`
 * from the same call. `isSystem` and `tags` decide which actions exist at all:
 * a platform-defined category can be deactivated but never edited or deleted, and
 * a category whose tags requests carry cannot be deleted.
 */
export type TagCategoryActionTarget = Pick<
  IntakeRequestTagCategoryDto,
  'id' | 'name' | 'description' | 'isActive' | 'isSystem' | 'tags'
>

/** How many requests carry a tag from the category — what deleting it would orphan. */
export const categoryRequestCount = (
  category: Pick<TagCategoryActionTarget, 'tags'>,
) => (category.tags ?? []).reduce((total, tag) => total + tag.requestCount, 0)
