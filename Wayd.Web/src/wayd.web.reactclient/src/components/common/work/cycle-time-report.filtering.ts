import { WorkItemListDto } from '@/src/services/api/wayd.schemas'

export type CycleTimeOutlierMethod = 'Balanced' | 'Forecasting'

export const getCycleTimeWorkItems = (
  workItemsData: WorkItemListDto[] | undefined,
): WorkItemListDto[] =>
  workItemsData?.filter((item) => item.cycleTime != null) ?? []

export const normalizePercentile = (percentile: number): number =>
  Math.max(0, Math.min(100, percentile)) / 100

export const sortCycleTimeWorkItems = (
  cycleTimeItems: WorkItemListDto[],
): WorkItemListDto[] =>
  [...cycleTimeItems].sort((a, b) => (a.cycleTime || 0) - (b.cycleTime || 0))

export const applyBalancedPercentileFilter = (
  sortedItems: WorkItemListDto[],
  percentile: number,
): WorkItemListDto[] => {
  const n = sortedItems.length
  const trimPercent = (1 - percentile) / 2
  const lowerIndex = Math.floor(n * trimPercent)
  const upperIndex = Math.ceil(n * (1 - trimPercent))
  return sortedItems.slice(lowerIndex, upperIndex)
}

export const applyForecastingPercentileFilter = (
  sortedItems: WorkItemListDto[],
  percentile: number,
): WorkItemListDto[] => {
  const n = sortedItems.length
  const cutoffIndex = Math.ceil(n * percentile)
  return sortedItems.slice(0, cutoffIndex)
}

export const filterCycleTimeWorkItems = (
  workItemsData: WorkItemListDto[] | undefined,
  percentile: number,
  method: CycleTimeOutlierMethod,
): WorkItemListDto[] => {
  const cycleTimeItems = getCycleTimeWorkItems(workItemsData)
  if (cycleTimeItems.length === 0) {
    return []
  }

  const normalizedPercentile = normalizePercentile(percentile)
  if (normalizedPercentile === 1) {
    return cycleTimeItems
  }
  if (normalizedPercentile === 0) {
    return []
  }

  const sorted = sortCycleTimeWorkItems(cycleTimeItems)

  if (method === 'Balanced') {
    return applyBalancedPercentileFilter(sorted, normalizedPercentile)
  }

  return applyForecastingPercentileFilter(sorted, normalizedPercentile)
}
