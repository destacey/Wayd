import { render, screen } from '@testing-library/react'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import ExpenditureCategoryPanel from './expenditure-category-panel'

const category = (
  overrides: Partial<ExpenditureCategoryDetailsDto> = {},
): ExpenditureCategoryDetailsDto => ({
  id: 4,
  name: 'Capital',
  description: 'Capitalized delivery spend',
  state: { id: 1, name: 'Active' },
  isCapitalizable: true,
  requiresDepreciation: false,
  accountingCode: '4100',
  ...overrides,
})

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('ExpenditureCategoryPanel', () => {
  it('renders the record fields', () => {
    // Arrange / Act
    render(<ExpenditureCategoryPanel expenditureCategory={category()} />)

    // Assert
    expect(valueFor('State')).toBe('Active')
    expect(valueFor('Accounting Code')).toBe('4100')
    expect(valueFor('Description')).toBe('Capitalized delivery spend')
  })

  it('renders booleans as Yes and No, agreeing with the grid', () => {
    // Arrange / Act — the list's columns use columnType 'yesNo', so a raw
    // "true"/"false" here would disagree with the row beside it.
    render(<ExpenditureCategoryPanel expenditureCategory={category()} />)

    // Assert
    expect(valueFor('Capitalizable')).toBe('Yes')
    expect(valueFor('Requires Depreciation')).toBe('No')
  })

  it('omits an absent accounting code rather than showing an empty label', () => {
    // Arrange / Act
    render(
      <ExpenditureCategoryPanel
        expenditureCategory={category({ accountingCode: undefined })}
      />,
    )

    // Assert
    expect(screen.queryByText('Accounting Code')).not.toBeInTheDocument()
  })

  it('omits an empty description', () => {
    // Arrange / Act
    render(
      <ExpenditureCategoryPanel
        expenditureCategory={category({ description: '' })}
      />,
    )

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('renders nothing while no record is selected', () => {
    // Arrange / Act
    const { container } = render(
      <ExpenditureCategoryPanel expenditureCategory={undefined} />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
