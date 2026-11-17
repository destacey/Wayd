import {
  applyBalancedPercentileFilter,
  applyForecastingPercentileFilter,
  filterCycleTimeWorkItems,
  getCycleTimeWorkItems,
  normalizePercentile,
  sortCycleTimeWorkItems,
} from './cycle-time-report.filtering'
import { WorkItemListDto } from '@/src/services/api/wayd.schemas'

const createMockWorkItem = (
  key: string,
  cycleTime: number | undefined,
): Partial<WorkItemListDto> => ({
  key,
  cycleTime,
  workspace: { key: 'ws-1' } as any,
})

describe('cycle-time-report.filtering', () => {
  describe('getCycleTimeWorkItems', () => {
    it('should return empty array for undefined input', () => {
      expect(getCycleTimeWorkItems(undefined)).toEqual([])
    })

    it('should return empty array for empty input', () => {
      expect(getCycleTimeWorkItems([])).toEqual([])
    })

    it('should keep only items with non-null cycle time', () => {
      const data = [
        createMockWorkItem('WI-1', undefined),
        createMockWorkItem('WI-2',  3),
        createMockWorkItem('WI-3',  undefined),
        createMockWorkItem('WI-4',  7),
      ] as WorkItemListDto[]

      const result = getCycleTimeWorkItems(data)
      expect(result.map((x) => x.key)).toEqual(['WI-2', 'WI-4'])
    })
  })

  describe('normalizePercentile', () => {
    it('should clamp negative values to 0', () => {
      expect(normalizePercentile(-10)).toBe(0)
    })

    it('should clamp values over 100 to 1', () => {
      expect(normalizePercentile(150)).toBe(1)
    })

    it('should convert boundary values correctly', () => {
      expect(normalizePercentile(0)).toBe(0)
      expect(normalizePercentile(100)).toBe(1)
    })

    it('should convert in-range values to decimal', () => {
      expect(normalizePercentile(60)).toBe(0.6)
    })
  })

  describe('sortCycleTimeWorkItems', () => {
    it('should return empty for empty array', () => {
      expect(sortCycleTimeWorkItems([])).toEqual([])
    })

    it('should sort unsorted items ascending by cycle time', () => {
      const unsorted = [
        createMockWorkItem('WI-1',  8),
        createMockWorkItem('WI-2',  2),
        createMockWorkItem('WI-3',  5),
      ] as WorkItemListDto[]

      const result = sortCycleTimeWorkItems(unsorted)
      expect(result.map((x) => x.key)).toEqual(['WI-2', 'WI-3', 'WI-1'])
    })

    it('should treat null or undefined cycle time as 0 for sorting', () => {
      const mixed = [
        createMockWorkItem('WI-1',  3),
        createMockWorkItem('WI-2', undefined),
        createMockWorkItem('WI-3',  undefined),
        createMockWorkItem('WI-4',  1),
      ] as WorkItemListDto[]

      const result = sortCycleTimeWorkItems(mixed)
      expect(result.map((x) => x.key)).toEqual(['WI-2', 'WI-3', 'WI-4', 'WI-1'])
    })
  })

  const sortedItems = [
    createMockWorkItem('WI-1',  1),
    createMockWorkItem('WI-2',  2),
    createMockWorkItem('WI-3',  3),
    createMockWorkItem('WI-4',  4),
    createMockWorkItem('WI-5',  100),
  ] as WorkItemListDto[]

  describe('applyBalancedPercentileFilter', () => {
    it('should trim both ends based on percentile', () => {
      const result = applyBalancedPercentileFilter(sortedItems, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-2', 'WI-3', 'WI-4'])
    })

    it('should keep single-item arrays for non-zero percentiles', () => {
      const singleItem = [createMockWorkItem('WI-1',  5)] as WorkItemListDto[]
      const result = applyBalancedPercentileFilter(singleItem, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-1'])
    })

    it('should keep both items in a two-item array at 60%', () => {
      const twoItems = [
        createMockWorkItem('WI-1',  1),
        createMockWorkItem('WI-2',  2),
      ] as WorkItemListDto[]
      const result = applyBalancedPercentileFilter(twoItems, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-2'])
    })

    it('should return empty for a two-item array at 0%', () => {
      const twoItems = [
        createMockWorkItem('WI-1',  1),
        createMockWorkItem('WI-2',  2),
      ] as WorkItemListDto[]
      const result = applyBalancedPercentileFilter(twoItems, 0)
      expect(result).toEqual([])
    })

    it('should keep all five items at 80% due floor/ceil boundary rounding', () => {
      const result = applyBalancedPercentileFilter(sortedItems, 0.8)
      expect(result.map((x) => x.key)).toEqual([
        'WI-1',
        'WI-2',
        'WI-3',
        'WI-4',
        'WI-5',
      ])
    })
  })

  describe('applyForecastingPercentileFilter', () => {
    it('should keep the fastest work items based on percentile', () => {
      const result = applyForecastingPercentileFilter(sortedItems, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-2', 'WI-3'])
    })

    it('should keep single-item arrays for non-zero percentiles', () => {
      const singleItem = [createMockWorkItem('WI-1',  5)] as WorkItemListDto[]
      const result = applyForecastingPercentileFilter(singleItem, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-1'])
    })

    it('should keep both items in a two-item array at 60%', () => {
      const twoItems = [
        createMockWorkItem('WI-1',  1),
        createMockWorkItem('WI-2',  2),
      ] as WorkItemListDto[]
      const result = applyForecastingPercentileFilter(twoItems, 0.6)
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-2'])
    })

    it('should keep four of five items at 80%', () => {
      const result = applyForecastingPercentileFilter(sortedItems, 0.8)
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-2', 'WI-3', 'WI-4'])
    })

    it('should keep all five items at 81% due ceil boundary rounding', () => {
      const result = applyForecastingPercentileFilter(sortedItems, 0.81)
      expect(result.map((x) => x.key)).toEqual([
        'WI-1',
        'WI-2',
        'WI-3',
        'WI-4',
        'WI-5',
      ])
    })
  })

  describe('filterCycleTimeWorkItems', () => {
    it('should return all cycle-time items when percentile is 100', () => {
      const data = [
        createMockWorkItem('WI-1',  5),
        createMockWorkItem('WI-2', undefined),
        createMockWorkItem('WI-3',  2),
      ] as WorkItemListDto[]

      const result = filterCycleTimeWorkItems(data, 100, 'Balanced')
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-3'])
    })

    it('should return empty when percentile is 0', () => {
      const result = filterCycleTimeWorkItems(sortedItems, 0, 'Balanced')
      expect(result).toEqual([])
    })

    it('should apply Balanced formula', () => {
      const result = filterCycleTimeWorkItems(sortedItems, 60, 'Balanced')
      expect(result.map((x) => x.key)).toEqual(['WI-2', 'WI-3', 'WI-4'])
    })

    it('should apply Forecasting formula', () => {
      const result = filterCycleTimeWorkItems(sortedItems, 60, 'Forecasting')
      expect(result.map((x) => x.key)).toEqual(['WI-1', 'WI-2', 'WI-3'])
    })
  })
})
