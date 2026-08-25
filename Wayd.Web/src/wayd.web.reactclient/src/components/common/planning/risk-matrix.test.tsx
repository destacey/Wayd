import { render, screen } from '@testing-library/react'
import RiskMatrix from './risk-matrix'

describe('RiskMatrix', () => {
  it('describes the risk for a screen reader, which cannot see the grid', () => {
    // Arrange / Act
    render(<RiskMatrix impact="High" likelihood="Medium" exposure="High" />)

    // Assert
    expect(
      screen.getByRole('img', {
        name: 'Risk matrix. Impact High, likelihood Medium, giving High exposure.',
      }),
    ).toBeInTheDocument()
  })

  it('marks exactly one cell', () => {
    // Arrange / Act
    const { container } = render(
      <RiskMatrix impact="Low" likelihood="Low" exposure="Low" />,
    )

    // Assert
    expect(container.querySelectorAll('[class*="marked"]')).toHaveLength(1)
  })

  it('renders nothing for a grade it cannot place', () => {
    // Arrange / Act — a marker in the wrong cell is worse than no grid, and
    // the grades come from the server as names rather than an enum.
    const { container } = render(
      <RiskMatrix impact="Catastrophic" likelihood="Low" exposure="High" />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders without an exposure, which is derived and may be absent', () => {
    // Arrange / Act
    render(<RiskMatrix impact="Low" likelihood="High" />)

    // Assert
    expect(screen.getByRole('img')).toBeInTheDocument()
    expect(screen.queryByText(/Exposure/)).toBeNull()
  })

  // The server derives exposure as Impact + Likelihood, banded at 4. If the
  // grid coloured cells by a different rule it would disagree with the
  // exposure printed beneath it.
  it.each([
    ['Low', 'Low', 'low'],
    ['Low', 'Medium', 'low'],
    ['Medium', 'Low', 'low'],
    ['Low', 'High', 'medium'],
    ['Medium', 'Medium', 'medium'],
    ['High', 'Low', 'medium'],
    ['Medium', 'High', 'high'],
    ['High', 'Medium', 'high'],
    ['High', 'High', 'high'],
  ])('bands %s impact x %s likelihood as %s', (impact, likelihood, band) => {
    // Arrange / Act
    const { container } = render(
      <RiskMatrix impact={impact} likelihood={likelihood} />,
    )

    // Assert
    const marked = container.querySelector('[class*="marked"]')
    expect(marked?.className).toContain(band)
  })
})
