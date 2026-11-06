import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'

/** The parts of a carried tag that decide how it reads and where it sorts. */
export interface TagText {
  name: string
  /** The tag's category, absent for a free tag. */
  categoryName?: string
}

/**
 * A tag as one line of text — "Platform | ios" for a tag in a category, the bare
 * name for a free tag. For grid cells, filters and tooltips, where a chip's
 * dimmed qualifier is not available.
 */
export const tagText = ({ name, categoryName }: TagText): string =>
  categoryName ? `${categoryName} | ${name}` : name

/**
 * The order a record's tags read in: tags in categories first, by category then name, and
 * free tags after them by name. Free tags last because the categories are the curated
 * filing everyone shares, and the informal labels are a team's own.
 */
export const compareTags = (a: TagText, b: TagText): number => {
  if (!a.categoryName !== !b.categoryName) return a.categoryName ? -1 : 1
  return (
    caseInsensitiveCompare(a.categoryName ?? '', b.categoryName ?? '') ||
    caseInsensitiveCompare(a.name, b.name)
  )
}
