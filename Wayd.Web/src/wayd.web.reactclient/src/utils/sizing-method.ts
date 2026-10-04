import { SizingMethod } from '@/src/services/wayd-api'

const labels: Record<SizingMethod, string> = {
  [SizingMethod.StoryPoints]: 'Story Points',
  [SizingMethod.Count]: 'Count',
  [SizingMethod.Effort]: 'Effort',
  [SizingMethod.Size]: 'Size',
}

/** The name a sizing method is shown under, such as "Story Points". */
export const sizingMethodLabel = (sizingMethod: SizingMethod): string =>
  labels[sizingMethod] ?? sizingMethod

/** Every sizing method, in display order, for a select. */
export const sizingMethodOptions = [
  SizingMethod.StoryPoints,
  SizingMethod.Effort,
  SizingMethod.Size,
  SizingMethod.Count,
].map((value) => ({ value, label: sizingMethodLabel(value) }))
