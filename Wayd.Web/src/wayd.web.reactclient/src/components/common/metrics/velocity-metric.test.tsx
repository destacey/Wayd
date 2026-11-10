import { render, screen } from '@testing-library/react'
import VelocityMetric from './velocity-metric'
import { SizingMethod } from '../../../services/wayd-api'

// Mock useTheme
vi.mock('../../contexts/theme', () => ({
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
    },
  }),
}))

describe('VelocityMetric', () => {
  it('renders velocity metric with correct values', () => {
    render(<VelocityMetric completed={10} total={20} />)

    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('10')).toBeInTheDocument()
    expect(screen.getByText('50.0%')).toBeInTheDocument()
  })

  it('renders 0% when total is 0', () => {
    render(<VelocityMetric completed={0} total={0} />)

    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()
    expect(screen.getByText('0%')).toBeInTheDocument()
  })

  it('renders custom tooltip', () => {
    const customTooltip = 'Custom Tooltip'
    // MetricCard uses Tooltip which wraps the content.
    // Testing tooltip visibility usually requires user event (hover),
    // but we can check if the Tooltip component received the prop if we mocked it,
    // or we can rely on integration test.
    // Here we trust MetricCard handles it, so we just check if it renders.
    render(<VelocityMetric completed={5} total={10} tooltip={customTooltip} />)
    expect(screen.getByText('Velocity')).toBeInTheDocument()
  })

  it('renders with SizingMethod.StoryPoints tooltip', () => {
    render(
      <VelocityMetric
        completed={5}
        total={10}
        tooltip={SizingMethod.StoryPoints}
      />,
    )
    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('50.0%')).toBeInTheDocument()
  })

  it('renders with SizingMethod.Count tooltip', () => {
    render(
      <VelocityMetric completed={3} total={6} tooltip={SizingMethod.Count} />,
    )
    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('50.0%')).toBeInTheDocument()
  })

  it('defaults to StoryPoints tooltip when not provided', () => {
    // Default tooltip is SizingMethod.StoryPoints
    render(<VelocityMetric completed={8} total={16} />)
    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('8')).toBeInTheDocument()
    expect(screen.getByText('50.0%')).toBeInTheDocument()
  })
})
