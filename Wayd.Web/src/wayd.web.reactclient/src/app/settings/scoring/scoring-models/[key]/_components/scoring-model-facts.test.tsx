import { render, screen } from '@testing-library/react'
import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import ScoringModelFacts from './scoring-model-facts'

const scoringModel = (
  overrides: Partial<ScoringModelDetailsDto> = {},
): ScoringModelDetailsDto =>
  ({
    id: 'c8d3e100-0000-0000-0000-000000000001',
    key: 1,
    name: 'WSJF',
    description: 'Weighted shortest job first',
    state: { id: 2, name: 'Active' },
    criteria: [],
    scales: [],
    outputs: [],
    ...overrides,
  }) as ScoringModelDetailsDto

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('ScoringModelFacts', () => {
  it('renders the record facts', () => {
    // Arrange / Act
    render(<ScoringModelFacts scoringModel={scoringModel()} />)

    // Assert
    expect(valueFor('Key')).toBe('1')
    expect(valueFor('State')).toBe('Active')
    expect(valueFor('Description')).toBe('Weighted shortest job first')
  })

  it('omits an empty description', () => {
    // Arrange / Act
    render(<ScoringModelFacts scoringModel={scoringModel({ description: '' })} />)

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('tolerates a model with no state', () => {
    // Arrange / Act — state is non-optional on the DTO but the old details tab
    // guarded it, so the data has evidently allowed it.
    render(
      <ScoringModelFacts
        scoringModel={scoringModel({ state: undefined as any })}
      />,
    )

    // Assert
    expect(screen.getByText('State')).toBeInTheDocument()
  })
})
