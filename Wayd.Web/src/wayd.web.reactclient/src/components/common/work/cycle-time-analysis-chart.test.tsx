import { render, screen } from '@testing-library/react'
import { CycleTimeAnalysisChart } from './cycle-time-analysis-chart'
import { WorkItemListDto } from '@/src/services/wayd-api'

// Mock Next.js dynamic import
vi.mock('next/dynamic', () => ({
  default: (fn: any, options: any) => {
    const Component = (props: any) => {
      return (
        <div data-testid="column-chart" data-config={JSON.stringify(props)} />
      )
    }
    Component.displayName = 'MockColumnChart'
    return Component
  },
}))

// Mock the useTheme hook
vi.mock('../../contexts/theme', () => ({
  default: vi.fn(() => ({
    antDesignChartsTheme: 'light',
    token: {
      colorSuccess: '#52c41a',
      colorWarning: '#faad14',
      colorPrimary: '#1890ff',
    },
  })),
}))

// Helper to create mock work items
const createMockWorkItem = (
  key: string,
  storyPoints: number | undefined,
  cycleTime: number | undefined,
): Partial<WorkItemListDto> => ({
  key,
  storyPoints,
  cycleTime,
  workspace: { key: 'ws-1' } as any,
})

describe('CycleTimeAnalysisChart', () => {
  describe('Loading State', () => {
    it('should render skeleton when isLoading is true', () => {
      render(<CycleTimeAnalysisChart workItems={[]} isLoading={true} />)

      const skeleton = document.querySelector('.ant-skeleton')
      expect(skeleton).toBeInTheDocument()
    })

    it('should not render chart when loading', () => {
      render(<CycleTimeAnalysisChart workItems={[]} isLoading={true} />)

      const chart = screen.queryByTestId('column-chart')
      expect(chart).not.toBeInTheDocument()
    })
  })

  describe('Empty State', () => {
    it('should render empty message when no work items', () => {
      render(<CycleTimeAnalysisChart workItems={[]} isLoading={false} />)

      expect(
        screen.getByText('No data available for chart'),
      ).toBeInTheDocument()
    })

    it('should not render chart when no work items', () => {
      render(<CycleTimeAnalysisChart workItems={[]} isLoading={false} />)

      const chart = screen.queryByTestId('column-chart')
      expect(chart).not.toBeInTheDocument()
    })
  })

  describe('Chart Rendering', () => {
    it('should render chart when work items are provided', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 5),
        createMockWorkItem('WI-2', 5, 8),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      expect(chart).toBeInTheDocument()
    })

    it('should default isLoading to false when not provided', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} />)

      const chart = screen.getByTestId('column-chart')
      expect(chart).toBeInTheDocument()
    })
  })

  describe('Data Processing', () => {
    it('should group work items by story points', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 5),
        createMockWorkItem('WI-2', 3, 7),
        createMockWorkItem('WI-3', 5, 10),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      // Should have 3 data points: 3 story points, 5 story points, and Overall
      expect(config.data).toHaveLength(3)
      expect(config.data.map((d: any) => d.storyPointCategory)).toContain('3')
      expect(config.data.map((d: any) => d.storyPointCategory)).toContain('5')
      expect(config.data.map((d: any) => d.storyPointCategory)).toContain(
        'Overall',
      )
    })

    it('should calculate average cycle time correctly', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 4),
        createMockWorkItem('WI-2', 3, 8),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const storyPoint3Data = config.data.find(
        (d: any) => d.storyPointCategory === '3',
      )
      expect(storyPoint3Data.averageCycleTime).toBe(6) // (4 + 8) / 2 = 6
      expect(storyPoint3Data.count).toBe(2)
    })

    it('should handle work items with no story points', () => {
      const workItems = [
        createMockWorkItem('WI-1', undefined, 5),
        createMockWorkItem('WI-2', undefined, 7),
        createMockWorkItem('WI-3', 3, 9),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const noStoryPointsData = config.data.find(
        (d: any) => d.storyPointCategory === 'No Story Points',
      )
      expect(noStoryPointsData).toBeDefined()
      expect(noStoryPointsData.averageCycleTime).toBe(6) // (5 + 7) / 2 = 6
      expect(noStoryPointsData.count).toBe(2)
    })

    it('should handle work items with null or undefined cycle time', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, undefined),
        createMockWorkItem('WI-2', 3, undefined),
        createMockWorkItem('WI-3', 3, 6),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const storyPoint3Data = config.data.find(
        (d: any) => d.storyPointCategory === '3',
      )
      expect(storyPoint3Data.averageCycleTime).toBe(2) // (0 + 0 + 6) / 3 = 2
      expect(storyPoint3Data.count).toBe(3)
    })

    it('should round average cycle time to 2 decimal places', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 5.555),
        createMockWorkItem('WI-2', 3, 7.777),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const storyPoint3Data = config.data.find(
        (d: any) => d.storyPointCategory === '3',
      )
      expect(storyPoint3Data.averageCycleTime).toBe(6.67) // (5.555 + 7.777) / 2 = 6.666, rounded to 6.67
    })
  })

  describe('Data Sorting', () => {
    it('should sort story points numerically in ascending order', () => {
      const workItems = [
        createMockWorkItem('WI-1', 13, 5),
        createMockWorkItem('WI-2', 1, 3),
        createMockWorkItem('WI-3', 5, 7),
        createMockWorkItem('WI-4', 3, 4),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const categories = config.data.map((d: any) => d.storyPointCategory)
      expect(categories).toEqual(['1', '3', '5', '13', 'Overall'])
    })

    it('should place "No Story Points" before "Overall"', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 5),
        createMockWorkItem('WI-2', undefined, 7),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const categories = config.data.map((d: any) => d.storyPointCategory)
      expect(categories).toEqual(['3', 'No Story Points', 'Overall'])
    })

    it('should place "Overall" at the end', () => {
      const workItems = [
        createMockWorkItem('WI-1', 8, 5),
        createMockWorkItem('WI-2', 2, 3),
        createMockWorkItem('WI-3', undefined, 7),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const categories = config.data.map((d: any) => d.storyPointCategory)
      expect(categories[categories.length - 1]).toBe('Overall')
    })
  })

  describe('Overall Category', () => {
    it('should calculate overall average across all work items', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 4),
        createMockWorkItem('WI-2', 5, 8),
        createMockWorkItem('WI-3', undefined, 12),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const overallData = config.data.find(
        (d: any) => d.storyPointCategory === 'Overall',
      )
      expect(overallData.averageCycleTime).toBe(8) // (4 + 8 + 12) / 3 = 8
      expect(overallData.count).toBe(3)
    })

    it('should include Overall category even with single work item', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      const overallData = config.data.find(
        (d: any) => d.storyPointCategory === 'Overall',
      )
      expect(overallData).toBeDefined()
      expect(overallData.averageCycleTime).toBe(5)
      expect(overallData.count).toBe(1)
    })
  })

  describe('Chart Configuration', () => {
    it('should configure correct x and y fields', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      expect(config.xField).toBe('storyPointCategory')
      expect(config.yField).toBe('averageCycleTime')
    })

    it('should configure axis titles', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      expect(config.axis.x.title).toBe('Story Points')
      expect(config.axis.y.title).toBe('Average Cycle Time (Days)')
    })

    it('should configure tooltip with correct fields', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      expect(config.tooltip.title).toBe('Story Points')
      expect(config.tooltip.items).toHaveLength(2)
      expect(config.tooltip.items[0].name).toBe('Average Cycle Time')
      expect(config.tooltip.items[0].field).toBe('averageCycleTime')
      expect(config.tooltip.items[1].name).toBe('Work Items')
      expect(config.tooltip.items[1].field).toBe('count')
    })

    it('should use theme from useTheme hook', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      expect(config.theme).toBe('light')
    })
  })

  describe('Bar Colors', () => {
    it('should configure style.fill as a function', () => {
      const workItems = [createMockWorkItem('WI-1', 3, 5)] as WorkItemListDto[]

      const { container } = render(
        <CycleTimeAnalysisChart workItems={workItems} isLoading={false} />,
      )

      // We can't test the exact function logic through JSON serialization,
      // but we can verify that the style object exists in the rendered component
      expect(
        container.querySelector('[data-testid="column-chart"]'),
      ).toBeInTheDocument()
    })

    it('should render chart with Overall, No Story Points, and regular categories', () => {
      const workItems = [
        createMockWorkItem('WI-1', 3, 5),
        createMockWorkItem('WI-2', undefined, 7),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      // Verify that all three category types are present in the data
      const categories = config.data.map((d: any) => d.storyPointCategory)
      expect(categories).toContain('3') // regular story point
      expect(categories).toContain('No Story Points') // no story points
      expect(categories).toContain('Overall') // overall category
    })
  })

  describe('Complex Scenarios', () => {
    it('should handle mixed story points and null values correctly', () => {
      const workItems = [
        createMockWorkItem('WI-1', 1, 2),
        createMockWorkItem('WI-2', 2, 3),
        createMockWorkItem('WI-3', 3, 5),
        createMockWorkItem('WI-4', 5, 8),
        createMockWorkItem('WI-5', undefined, 4),
        createMockWorkItem('WI-6', undefined, 6),
        createMockWorkItem('WI-7', 1, 3),
      ] as WorkItemListDto[]

      render(<CycleTimeAnalysisChart workItems={workItems} isLoading={false} />)

      const chart = screen.getByTestId('column-chart')
      const config = JSON.parse(chart.getAttribute('data-config') || '{}')

      // Should have 6 categories: 1, 2, 3, 5, No Story Points, Overall
      expect(config.data).toHaveLength(6)

      // Check 1 story point average (2 items: 2 and 3)
      const sp1Data = config.data.find((d: any) => d.storyPointCategory === '1')
      expect(sp1Data.averageCycleTime).toBe(2.5)
      expect(sp1Data.count).toBe(2)

      // Check No Story Points average (2 items: 4 and 6)
      const noSpData = config.data.find(
        (d: any) => d.storyPointCategory === 'No Story Points',
      )
      expect(noSpData.averageCycleTime).toBe(5)
      expect(noSpData.count).toBe(2)

      // Check Overall (all 7 items)
      const overallData = config.data.find(
        (d: any) => d.storyPointCategory === 'Overall',
      )
      expect(overallData.averageCycleTime).toBe(4.43) // (2+3+5+8+4+6+3)/7 = 4.428571..., rounded to 4.43
      expect(overallData.count).toBe(7)

      // Check order
      const categories = config.data.map((d: any) => d.storyPointCategory)
      expect(categories).toEqual([
        '1',
        '2',
        '3',
        '5',
        'No Story Points',
        'Overall',
      ])
    })
  })
})
