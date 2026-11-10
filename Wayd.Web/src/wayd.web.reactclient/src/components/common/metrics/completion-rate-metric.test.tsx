import { render, screen } from '@testing-library/react'
import CompletionRateMetric from './completion-rate-metric'
import { SizingMethod } from '../../../services/wayd-api'

// Mock useTheme
vi.mock('../../contexts/theme', () => ({
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
    },
  }),
}))

describe('CompletionRateMetric', () => {
  it('renders completion rate correctly', () => {
    render(<CompletionRateMetric completed={50} total={100} />)

    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
    // Ant Design Statistic splits integer and decimal parts
    expect(screen.getByText('50')).toBeInTheDocument()
    expect(screen.getByText('.0')).toBeInTheDocument()
    expect(screen.getByText('%')).toBeInTheDocument()
  })

  it('renders 0% when total is 0', () => {
    render(<CompletionRateMetric completed={0} total={0} />)

    expect(screen.getByText('0')).toBeInTheDocument()
    expect(screen.getByText('.0')).toBeInTheDocument()
  })

  it('calculates percentage correctly', () => {
    render(<CompletionRateMetric completed={1} total={3} />)
    // 1/3 = 33.333... -> 33.3
    expect(screen.getByText('33')).toBeInTheDocument()
    expect(screen.getByText('.3')).toBeInTheDocument()
  })

  it('renders with SizingMethod.StoryPoints tooltip', () => {
    render(
      <CompletionRateMetric
        completed={5}
        total={10}
        tooltip={SizingMethod.StoryPoints}
      />,
    )
    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
    expect(screen.getByText('50')).toBeInTheDocument()
  })

  it('renders with SizingMethod.Count tooltip', () => {
    render(
      <CompletionRateMetric
        completed={3}
        total={6}
        tooltip={SizingMethod.Count}
      />,
    )
    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
    expect(screen.getByText('50')).toBeInTheDocument()
  })

  it('renders with custom string tooltip', () => {
    render(
      <CompletionRateMetric
        completed={5}
        total={10}
        tooltip="Custom tooltip text"
      />,
    )
    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
  })

  it('defaults to StoryPoints tooltip when not provided', () => {
    render(<CompletionRateMetric completed={8} total={16} />)
    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
    expect(screen.getByText('50')).toBeInTheDocument()
  })
})
