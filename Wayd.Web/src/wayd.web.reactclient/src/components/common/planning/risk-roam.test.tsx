import { render, screen } from '@testing-library/react'
import RiskRoam from './risk-roam'

const mockCategories = jest.fn()

jest.mock('@/src/store/features/planning/risks-api', () => ({
  useGetRiskCategoriesQuery: () => mockCategories(),
}))

const CATEGORIES = [
  { id: 1, name: 'Resolved', description: 'Not a threat at this time.', order: 1 },
  { id: 2, name: 'Owned', description: 'Someone owns handling it.', order: 2 },
  { id: 3, name: 'Accepted', description: 'Accepted as-is.', order: 3 },
  { id: 4, name: 'Mitigated', description: 'Actions were taken.', order: 4 },
]

describe('RiskRoam', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockCategories.mockReturnValue({ data: CATEGORIES, isLoading: false })
  })

  it('shows every option, not only the one chosen', () => {
    // Arrange / Act — ROAM is a decision, so the alternatives are part of what
    // the answer means.
    render(<RiskRoam category="Owned" />)

    // Assert
    CATEGORIES.forEach((c) =>
      expect(screen.getByText(c.name)).toBeInTheDocument(),
    )
  })

  it('marks the chosen one', () => {
    // Arrange / Act
    render(<RiskRoam category="Owned" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    const current = items.filter((i) => i.getAttribute('aria-current') === 'true')
    expect(current).toHaveLength(1)
    expect(current[0]).toHaveTextContent('Owned')
  })

  it('matches the category regardless of casing', () => {
    // Arrange / Act — the name arrives from the server, not an enum.
    render(<RiskRoam category="owned" />)

    // Assert
    expect(
      screen.getAllByRole('listitem').filter(
        (i) => i.getAttribute('aria-current') === 'true',
      ),
    ).toHaveLength(1)
  })

  it('marks nothing when the category is unrecognised', () => {
    // Arrange / Act — marking the wrong option is worse than marking none.
    render(<RiskRoam category="Deferred" />)

    // Assert
    expect(
      screen.getAllByRole('listitem').filter(
        (i) => i.getAttribute('aria-current') === 'true',
      ),
    ).toHaveLength(0)
  })

  it('renders nothing when the categories cannot be loaded', () => {
    // Arrange
    mockCategories.mockReturnValue({ data: undefined, isLoading: false })

    // Act
    const { container } = render(<RiskRoam category="Owned" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
