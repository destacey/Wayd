import { render, screen } from '@testing-library/react'
import WaydStatisticNumber from './wayd-statistic-number'

describe('WaydStatisticNumber', () => {
  it('renders the number correctly', () => {
    render(<WaydStatisticNumber value={12345.67} />)
    expect(screen.getByText('12,345.67')).toBeInTheDocument()
  })

  it('renders nothing when value is null', () => {
    const { container } = render(
      <WaydStatisticNumber value={null as unknown as undefined} />,
    )
    expect(container.firstChild).toBeNull()
  })

  it('renders nothing when value is undefined', () => {
    const { container } = render(<WaydStatisticNumber value={undefined} />)
    expect(container.firstChild).toBeNull()
  })

  it('renders 0 correctly', () => {
    render(<WaydStatisticNumber value={0} />)
    const numberElement = screen.getByText('0')
    expect(numberElement).toBeInTheDocument()
  })

  it('renders the number with the specified precision (no rounding)', () => {
    render(<WaydStatisticNumber value={12345.6789} precision={2} />)
    expect(screen.getByText('12,345.67')).toBeInTheDocument()
  })

  it.each([
    { value: 1234567, precision: undefined, expected: '1,234,567' },
    { value: 999, precision: undefined, expected: '999' },
    { value: -1234567.5, precision: undefined, expected: '-1,234,567.5' },
    { value: 0.123456, precision: undefined, expected: '0.123456' },
    { value: '9876543.21', precision: undefined, expected: '9,876,543.21' },
    { value: '.5', precision: undefined, expected: '0.5' },
    { value: 1234.5, precision: 3, expected: '1,234.500' },
    { value: 1234, precision: 2, expected: '1,234.00' },
    { value: 1234.99, precision: 0, expected: '1,234' },
  ])(
    'renders $value at precision $precision as $expected',
    ({ value, precision, expected }) => {
      // Arrange / Act
      const { container } = render(
        <WaydStatisticNumber value={value} precision={precision} />,
      )

      // Assert
      expect(container.textContent).toBe(expected)
    },
  )

  it.each(['n/a', '1e21', '-', '1,234'])(
    'renders %s as written when it is not a plain decimal number',
    (value) => {
      // Arrange / Act
      const { container } = render(<WaydStatisticNumber value={value} />)

      // Assert
      expect(container.textContent).toBe(value)
    },
  )
})
