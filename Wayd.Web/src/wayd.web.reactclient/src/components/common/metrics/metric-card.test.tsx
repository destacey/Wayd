import { render, screen } from '@testing-library/react'
import MetricCard from './metric-card'

describe('MetricCard', () => {
  it('renders with basic title and value', () => {
    render(<MetricCard title="Total Users" value={1234} />)

    expect(screen.getByText('Total Users')).toBeInTheDocument()
    // Ant Design Statistic formats numbers with commas
    expect(screen.getByText('1,234')).toBeInTheDocument()
  })

  it('renders with string value', () => {
    render(<MetricCard title="Status" value="Active" />)

    expect(screen.getByText('Status')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('renders with precision', () => {
    render(<MetricCard title="Average Score" value={85.6789} precision={2} />)

    expect(screen.getByText('Average Score')).toBeInTheDocument()
    // Ant Design Statistic splits integer and decimal parts, rounds to 2 decimals
    expect(screen.getByText(/85/)).toBeInTheDocument()
    expect(screen.getByText(/\.67/)).toBeInTheDocument()
  })

  it('renders with suffix', () => {
    render(<MetricCard title="Completion Rate" value={95} suffix="%" />)

    expect(screen.getByText('Completion Rate')).toBeInTheDocument()
    expect(screen.getByText('95')).toBeInTheDocument()
    expect(screen.getByText('%')).toBeInTheDocument()
  })

  it('renders with prefix', () => {
    render(<MetricCard title="Revenue" value={50000} prefix="$" />)

    expect(screen.getByText('Revenue')).toBeInTheDocument()
    expect(screen.getByText('50,000')).toBeInTheDocument()
    expect(screen.getByText('$')).toBeInTheDocument()
  })

  it('renders with both prefix and suffix', () => {
    render(
      <MetricCard
        title="Price Change"
        value={25.5}
        prefix="$"
        suffix="/mo"
        precision={2}
      />,
    )

    expect(screen.getByText('Price Change')).toBeInTheDocument()
    expect(screen.getByText(/25/)).toBeInTheDocument()
    expect(screen.getByText(/50/)).toBeInTheDocument()
    expect(screen.getByText('$')).toBeInTheDocument()
    expect(screen.getByText('/mo')).toBeInTheDocument()
  })

  it('applies default card style when not provided', () => {
    const { container } = render(
      <MetricCard title="Default Style" value={100} />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({ height: '100%' })
  })

  it('applies custom card style when provided', () => {
    const customStyle = { backgroundColor: 'rgb(173, 216, 230)', padding: '20px' }
    const { container } = render(
      <MetricCard title="Custom Card" value={200} cardStyle={customStyle} />,
    )

    const card = container.querySelector('.ant-card')
    expect(card).toHaveStyle({
      backgroundColor: 'rgb(173, 216, 230)',
      padding: '20px',
    })
  })

  it('applies default statistic style when not provided', () => {
    const { container } = render(
      <MetricCard title="Default Statistic" value={300} />,
    )

    const statistic = container.querySelector('.ant-statistic')
    expect(statistic).toHaveStyle({ whiteSpace: 'nowrap' })
  })

  it('applies custom statistic style when provided', () => {
    const customStyle = { fontSize: '24px', color: 'rgb(255, 0, 0)' }
    const { container } = render(
      <MetricCard
        title="Custom Statistic"
        value={400}
        statisticStyle={customStyle}
      />,
    )

    const statistic = container.querySelector('.ant-statistic')
    expect(statistic).toHaveStyle({
      fontSize: '24px',
      color: 'rgb(255, 0, 0)',
    })
  })

  it('applies custom value style', () => {
    const valueStyle = {
      color: 'rgb(0, 128, 0)',
    }
    const { container } = render(
      <MetricCard
        title="Custom Value Style"
        value={500}
        valueStyle={valueStyle}
      />,
    )

    const value = container.querySelector('.ant-statistic-content-value')
    expect(value).toHaveStyle({
      color: 'rgb(0, 128, 0)',
    })
  })

  it('renders loading state', () => {
    const { container } = render(
      <MetricCard title="Loading" value={600} loading />,
    )

    const skeleton = container.querySelector('.ant-skeleton')
    expect(skeleton).toBeInTheDocument()
  })

  it('passes through all StatisticProps', () => {
    const valueRender = (node: React.ReactNode) => (
      <span data-testid="custom-render">{node}</span>
    )

    render(
      <MetricCard
        title="All Props"
        value={700}
        prefix=">"
        suffix="<"
        precision={1}
        valueRender={valueRender}
      />,
    )

    expect(screen.getByTestId('custom-render')).toBeInTheDocument()
    expect(screen.getByText(/700/)).toBeInTheDocument()
    expect(screen.getByText('>')).toBeInTheDocument()
    expect(screen.getByText('<')).toBeInTheDocument()
  })

  it('handles zero value correctly', () => {
    render(<MetricCard title="Zero Value" value={0} />)

    expect(screen.getByText('Zero Value')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()
  })

  it('handles negative value correctly', () => {
    render(<MetricCard title="Negative Value" value={-42} />)

    expect(screen.getByText('Negative Value')).toBeInTheDocument()
    // Negative sign is part of the value
    expect(screen.getByText(/-42/)).toBeInTheDocument()
  })

  it('handles large numbers correctly', () => {
    render(<MetricCard title="Large Number" value={1234567890} />)

    expect(screen.getByText('Large Number')).toBeInTheDocument()
    // Ant Design formats large numbers with commas
    expect(screen.getByText('1,234,567,890')).toBeInTheDocument()
  })

  it('renders without tooltip when not provided', () => {
    const { container } = render(<MetricCard title="No Tooltip" value={100} />)

    // Statistic should be directly in the card body
    const statistic = container.querySelector('.ant-statistic')
    expect(statistic?.parentElement).toHaveClass('ant-card-body')
  })

  it('renders with tooltip when provided', () => {
    render(
      <MetricCard
        title="With Tooltip"
        value={200}
        tooltip="This is helpful information"
      />,
    )

    // Verify the metric card renders with a title and value
    expect(screen.getByText('With Tooltip')).toBeInTheDocument()
    expect(screen.getByText('200')).toBeInTheDocument()
  })

  it('renders with secondary value', () => {
    render(
      <MetricCard title="Velocity" value={26} secondaryValue="56.5%" />,
    )

    expect(screen.getByText('Velocity')).toBeInTheDocument()
    expect(screen.getByText('26')).toBeInTheDocument()
    expect(screen.getByText('56.5%')).toBeInTheDocument()
  })

  it('renders with numeric secondary value', () => {
    render(
      <MetricCard title="In Progress" value={20} secondaryValue={42.5} />,
    )

    expect(screen.getByText('In Progress')).toBeInTheDocument()
    expect(screen.getByText('20')).toBeInTheDocument()
    expect(screen.getByText('42.5')).toBeInTheDocument()
  })

  it('does not render secondary value when not provided', () => {
    const { container } = render(<MetricCard title="No Secondary" value={100} />)

    expect(screen.getByText('No Secondary')).toBeInTheDocument()
    expect(screen.getByText('100')).toBeInTheDocument()

    // Card.Meta should not be rendered when no secondary value
    const meta = container.querySelector('.ant-card-meta')
    expect(meta).not.toBeInTheDocument()
  })

  it('renders secondary value within Card.Meta', () => {
    const { container } = render(
      <MetricCard title="Default Secondary" value={50} secondaryValue="25%" />,
    )

    const meta = container.querySelector('.ant-card-meta')
    expect(meta).toBeInTheDocument()
    expect(screen.getByText('25%')).toBeInTheDocument()
  })

  it('gives cards a matching floor whether or not they have a secondary value', () => {
    // Arrange — antd stretches Cols within a line but not across a wrap, so
    // without a floor a card with a qualifier is taller than its neighbours.
    const { container: plain } = render(
      <MetricCard title="Plain" value={1} />,
    )
    const { container: withSecondary } = render(
      <MetricCard title="Qualified" value={1} secondaryValue="Last 90 days" />,
    )

    // Assert
    const plainCard = plain.querySelector('.ant-card') as HTMLElement
    const qualifiedCard = withSecondary.querySelector(
      '.ant-card',
    ) as HTMLElement
    expect(plainCard.style.minHeight).toBe(qualifiedCard.style.minHeight)
    expect(plainCard.style.minHeight).not.toBe('')
  })

  it('leaves an embedded metric unconstrained — it is nested in another card', () => {
    // Arrange / Act
    const { container } = render(
      <MetricCard title="Nested" value={1} embedded />,
    )

    // Assert
    expect(container.querySelector('.ant-card')).toBeNull()
  })

  it('renders secondary value with tooltip', () => {
    render(
      <MetricCard
        title="With Both"
        value={100}
        secondaryValue="100%"
        tooltip="Complete information"
      />,
    )

    expect(screen.getByText('With Both')).toBeInTheDocument()
    expect(screen.getByText('100')).toBeInTheDocument()
    expect(screen.getByText('100%')).toBeInTheDocument()
  })
})
