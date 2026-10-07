import { render, screen } from '@testing-library/react'
import HealthMetric from './health-metric'

// Mock useTheme
jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
      colorWarning: '#faad14',
      colorError: '#ff4d4f',
    },
  }),
}))

const valueColor = (container: HTMLElement) =>
  (container.querySelector('.ant-statistic-content') as HTMLElement | null)
    ?.style.color

describe('HealthMetric', () => {
  it('renders health metric correctly', () => {
    render(<HealthMetric value={5} title="Issues" />)
    expect(screen.getByText('Issues')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('shows a bad value as an error by default', () => {
    // Arrange / Act
    const { container } = render(<HealthMetric value={5} title="Issues" />)

    // Assert
    expect(valueColor(container)).toBe('rgb(255, 77, 79)')
  })

  it('shows a bad value as a warning when asked', () => {
    // Arrange / Act
    const { container } = render(
      <HealthMetric value={5} title="Issues" severity="warning" />,
    )

    // Assert
    expect(valueColor(container)).toBe('rgb(250, 173, 20)')
  })

  it('shows a good value as success whatever the severity', () => {
    // Arrange / Act
    const { container } = render(
      <HealthMetric value={0} title="Issues" severity="warning" />,
    )

    // Assert
    expect(valueColor(container)).toBe('rgb(82, 196, 26)')
  })
})
