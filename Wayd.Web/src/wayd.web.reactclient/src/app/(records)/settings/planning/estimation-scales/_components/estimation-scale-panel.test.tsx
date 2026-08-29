import { render, screen } from '@testing-library/react'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import EstimationScalePanel from './estimation-scale-panel'

const scale = (
  overrides: Partial<EstimationScaleDto> = {},
): EstimationScaleDto => ({
  id: 3,
  name: 'Fibonacci',
  description: 'Classic planning poker deck',
  isActive: true,
  values: ['1', '2', '3', '5', '8'],
  ...overrides,
})

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('EstimationScalePanel', () => {
  it('renders the record fields', () => {
    // Arrange / Act
    render(<EstimationScalePanel estimationScale={scale()} />)

    // Assert
    expect(valueFor('Active')).toBe('Yes')
    expect(valueFor('Description')).toBe('Classic planning poker deck')
  })

  it('renders each value as its own tag', () => {
    // Arrange / Act — the values are the scale, so they belong in the panel
    // rather than behind a section
    render(<EstimationScalePanel estimationScale={scale()} />)

    // Assert
    scale().values.forEach((value) =>
      expect(screen.getByText(value)).toBeInTheDocument(),
    )
  })

  it('renders an inactive scale as No', () => {
    // Arrange / Act
    render(<EstimationScalePanel estimationScale={scale({ isActive: false })} />)

    // Assert
    expect(valueFor('Active')).toBe('No')
  })

  it('omits an empty description', () => {
    // Arrange / Act
    render(
      <EstimationScalePanel estimationScale={scale({ description: undefined })} />,
    )

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('handles a scale with no values', () => {
    // Arrange / Act — the label stays, since an empty scale is a real state
    // worth seeing rather than a missing field
    render(<EstimationScalePanel estimationScale={scale({ values: [] })} />)

    // Assert
    expect(screen.getByText('Values')).toBeInTheDocument()
  })

  it('renders nothing while no record is selected', () => {
    // Arrange / Act
    const { container } = render(
      <EstimationScalePanel estimationScale={undefined} />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
