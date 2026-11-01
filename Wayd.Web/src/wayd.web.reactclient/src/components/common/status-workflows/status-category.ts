import { StatusCategory, StatusCategoryLabelDto } from '@/src/services/wayd-api'

/** The categories in their numeric id order, as `SimpleNavigationDto` ids carry them. */
const CATEGORIES_BY_ID: readonly StatusCategory[] = [
  StatusCategory.Proposed,
  StatusCategory.Active,
  StatusCategory.Done,
  StatusCategory.Removed,
]

/**
 * The category a category navigation's id stands for.
 *
 * Read the id, never the name: the name is the owner type's label for the category ("Shipped"), which
 * is not a category value.
 */
export const statusCategoryFromId = (
  id: number | undefined,
): StatusCategory | undefined =>
  id === undefined ? undefined : CATEGORIES_BY_ID[id]

/**
 * What an owner type calls `category`, from the labels it declares; the category's own name when it
 * declares none or the labels have not loaded.
 */
export const statusCategoryLabel = (
  labels: readonly StatusCategoryLabelDto[] | undefined,
  category: StatusCategory,
): string => labels?.find((l) => l.category === category)?.label ?? category

/** Select options for the four categories, named as the owner type labels them. */
export const statusCategoryOptions = (
  labels: readonly StatusCategoryLabelDto[] | undefined,
) =>
  CATEGORIES_BY_ID.map((c) => ({
    value: c,
    label: statusCategoryLabel(labels, c),
  }))

const MEANINGS: Record<StatusCategory, string> = {
  [StatusCategory.Proposed]: 'not started yet.',
  [StatusCategory.Active]: 'in progress.',
  [StatusCategory.Done]: 'completed successfully.',
  [StatusCategory.Removed]: 'abandoned or withdrawn, not completed.',
}

/**
 * What a status's category means, for a reader who does not know the workflow.
 *
 * A status name is the workflow's own word and an administrator can rename it to anything; the
 * category is the fixed meaning behind it, and what rollups and filters group on. Without it,
 * "Shipped" and "Shelved" are equally opaque on a workflow you have not seen before. The category is
 * named as the record's owner type labels it, and the meaning that follows is always the category's.
 */
export const statusCategoryDescription = (status: {
  category: StatusCategory
  categoryLabel?: string
}): string => {
  const meaning = MEANINGS[status.category]
  if (!meaning) return ''
  return `${status.categoryLabel || status.category} — ${meaning}`
}
