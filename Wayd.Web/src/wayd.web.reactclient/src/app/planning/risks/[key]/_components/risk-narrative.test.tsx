import { render, screen } from '@testing-library/react'
import RiskNarrative from './risk-narrative'

// RiskRoam fetches the ROAM categories; it has its own suite, and pulling a
// store in here would test the wiring rather than this component.
jest.mock('@/src/components/common/planning/risk-roam', () => {
  const RiskRoam = ({ category }: { category?: string }) => (
    <div>roam: {category}</div>
  )
  return RiskRoam
})

const risk = {
  id: 'risk-1',
  key: 214,
  summary: 'Token refresh may exceed the service cap',
  impact: { id: '3', name: 'High' },
  likelihood: { id: '4', name: 'Medium' },
  exposure: { id: '5', name: 'High' },
} as any

describe('RiskNarrative', () => {
  it('plots the risk on the matrix', () => {
    // Arrange / Act
    render(<RiskNarrative risk={risk} />)

    // Assert
    expect(
      screen.getByRole('img', { name: /Impact High, likelihood Medium/i }),
    ).toBeInTheDocument()
    // "High" is also an axis label, so match the exposure line as a whole.
    expect(screen.getByText(/Exposure/)).toBeInTheDocument()
  })

  it('says so when the prose is missing rather than rendering a blank', () => {
    // Arrange / Act — a risk with no response is worth noticing.
    render(<RiskNarrative risk={risk} />)

    // Assert
    expect(screen.getByText('No description provided.')).toBeInTheDocument()
    expect(screen.getByText('No response recorded.')).toBeInTheDocument()
  })

  it('renders the prose instead of the empty state when it is there', () => {
    // Arrange / Act
    render(
      <RiskNarrative
        risk={{
          ...risk,
          description: 'Refresh storms during peak load.',
          response: 'Add a circuit breaker before Q3.',
        }}
      />,
    )

    // Assert
    // ReactMarkdown is mocked globally, so the prose itself is not in the DOM
    // — what is observable is that neither empty state is shown.
    expect(screen.queryByText('No description provided.')).toBeNull()
    expect(screen.queryByText('No response recorded.')).toBeNull()
  })

  it('heads the content with the ROAM decision', () => {
    // Arrange / Act — it frames how the description and response read.
    render(<RiskNarrative risk={risk} />)

    // Assert
    expect(screen.getByText(/roam:/)).toBeInTheDocument()
  })
})
