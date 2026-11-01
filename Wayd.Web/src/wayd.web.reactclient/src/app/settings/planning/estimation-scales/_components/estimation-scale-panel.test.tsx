import { render, screen } from '@testing-library/react'
import {
  EstimationScaleDto,
  EstimationScaleVersionDto,
} from '@/src/services/wayd-api'
import EstimationScalePanel from './estimation-scale-panel'

const version = (
  overrides: Partial<EstimationScaleVersionDto> = {},
): EstimationScaleVersionDto => ({
  id: 'version-2',
  number: 2,
  publishedOn: new Date('2026-03-04T12:00:00Z'),
  values: [
    { value: '1', isEstimate: true },
    { value: '2', isEstimate: true },
    { value: '3', isEstimate: true },
    { value: '?', isEstimate: false },
  ],
  ...overrides,
})

const scale = (
  overrides: Partial<EstimationScaleDto> = {},
): EstimationScaleDto => ({
  id: 'scale-3',
  key: 3,
  name: 'Fibonacci',
  description: 'Classic planning poker deck',
  isActive: true,
  currentVersion: version(),
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
    expect(valueFor('Version')).toBe('2')
  })

  it('renders estimates and cards under their own labels', () => {
    // Arrange / Act — only an estimate can become a size, so the two are told
    // apart rather than shown as one row
    render(<EstimationScalePanel estimationScale={scale()} />)

    // Assert
    expect(valueFor('Estimates')).toBe('123')
    expect(valueFor('Cards')).toBe('?')
  })

  it('omits the cards label when the version has none', () => {
    // Arrange
    const noCards = version({
      values: [
        { value: 'S', isEstimate: true },
        { value: 'M', isEstimate: true },
      ],
    })

    // Act
    render(
      <EstimationScalePanel estimationScale={scale({ currentVersion: noCards })} />,
    )

    // Assert
    expect(valueFor('Estimates')).toBe('SM')
    expect(screen.queryByText('Cards')).not.toBeInTheDocument()
  })

  it('lists the earlier versions, leaving out the current one', () => {
    // Arrange
    const current = version()
    const first = version({
      id: 'version-1',
      number: 1,
      publishedOn: new Date('2026-01-10T12:00:00Z'),
      values: [
        { value: 'S', isEstimate: true },
        { value: 'L', isEstimate: true },
      ],
    })

    // Act
    render(
      <EstimationScalePanel
        estimationScale={scale({ currentVersion: current })}
        versions={[current, first]}
      />,
    )

    // Assert
    expect(screen.getByText('Version 1 · Jan 10, 2026')).toBeInTheDocument()
    expect(screen.queryByText(/^Version 2 ·/)).not.toBeInTheDocument()
    expect(screen.getByText('L')).toBeInTheDocument()
  })

  it('omits the history while a scale has only one version', () => {
    // Arrange / Act
    render(
      <EstimationScalePanel estimationScale={scale()} versions={[version()]} />,
    )

    // Assert
    expect(screen.queryByText('Earlier Versions')).not.toBeInTheDocument()
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

  it('renders nothing while no record is selected', () => {
    // Arrange / Act
    const { container } = render(
      <EstimationScalePanel estimationScale={undefined} />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
