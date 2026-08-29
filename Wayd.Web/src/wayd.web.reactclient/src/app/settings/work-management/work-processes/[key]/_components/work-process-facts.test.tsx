import { render, screen } from '@testing-library/react'
import { WorkProcessDto } from '@/src/services/wayd-api'
import WorkProcessFacts from './work-process-facts'

const workProcess = (
  overrides: Partial<WorkProcessDto> = {},
): WorkProcessDto => ({
  id: 'a4f1c2e0-0000-0000-0000-000000000001',
  key: 12,
  name: 'Agile Delivery',
  description: 'The default process for delivery teams',
  ownership: { id: 1, name: 'Wayd' },
  isActive: true,
  ...overrides,
})

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('WorkProcessFacts', () => {
  it('renders the record facts', () => {
    // Arrange / Act
    render(<WorkProcessFacts workProcess={workProcess()} />)

    // Assert
    expect(valueFor('Key')).toBe('12')
    expect(valueFor('Ownership')).toBe('Wayd')
    expect(valueFor('Description')).toBe(
      'The default process for delivery teams',
    )
  })

  it('renders Active as Yes or No rather than a raw boolean', () => {
    // Arrange / Act
    render(<WorkProcessFacts workProcess={workProcess({ isActive: false })} />)

    // Assert
    expect(valueFor('Active')).toBe('No')
  })

  it('omits an absent description rather than showing an empty label', () => {
    // Arrange / Act
    render(
      <WorkProcessFacts workProcess={workProcess({ description: undefined })} />,
    )

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('keeps the key in the facts, since the chip carries it too', () => {
    // Arrange / Act — the identity bar shows the key as its chip; the facts
    // repeat it so the panel is complete on its own at mobile widths, where
    // the facts render inline.
    render(<WorkProcessFacts workProcess={workProcess()} />)

    // Assert
    expect(screen.getByText('Key')).toBeInTheDocument()
  })
})
