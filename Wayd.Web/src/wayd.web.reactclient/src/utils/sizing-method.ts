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

const measures: Record<SizingMethod, string> = {
  [SizingMethod.StoryPoints]: 'story points',
  [SizingMethod.Count]: 'work items',
  [SizingMethod.Effort]: 'effort',
  [SizingMethod.Size]: 'size',
}

/** What a sizing method measures, for use mid-sentence: "story points", or "work items" under Count. */
export const sizingMethodMeasure = (sizingMethod: SizingMethod): string =>
  measures[sizingMethod] ?? sizingMethod

/** Whether a value is a sizing method rather than other text. */
export const isSizingMethod = (value: unknown): value is SizingMethod =>
  Object.values(SizingMethod).includes(value as SizingMethod)

/** Every sizing method, in display order, for a select. */
export const sizingMethodOptions = [
  SizingMethod.StoryPoints,
  SizingMethod.Effort,
  SizingMethod.Size,
  SizingMethod.Count,
].map((value) => ({ value, label: sizingMethodLabel(value) }))
