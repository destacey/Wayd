import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  DependencyStrength,
  ProductDependenciesDto,
  ProductDependencyDto,
  ProductDto,
} from '@/src/services/wayd-api'
import { DEPENDENCY_STRENGTH_FILTER_KEY } from '../../../_components/dependency-map/use-dependency-strength-filter'
import ProductOverview from './product-overview'

// The version tile counts back from today, and the global setup mocks dayjs down to formatting.
jest.unmock('dayjs')

// The canvas measures itself and reads the theme provider, neither of which this test supplies. The stub
// renders what the overview hands it, which is what this suite is about; the map's own tests cover drawing.
jest.mock('../../../_components/dependency-map/dependency-map', () => ({
  __esModule: true,
  default: ({
    nodes,
    edges,
    filters,
    emptyText,
  }: {
    nodes: { id: string }[]
    edges: { id: string }[]
    filters?: React.ReactNode
    emptyText?: React.ReactNode
  }) => (
    <div data-testid="dependency-map">
      {filters}
      <span data-testid="map-edges">{edges.map((e) => e.id).join(',')}</span>
      {nodes.length === 0 && emptyText}
    </div>
  ),
}))

const self = { id: 'product-1', key: 7, name: 'Storefront' }

const product = {
  ...self,
  type: { id: 'type-1', key: 1, name: 'Application' },
  status: { id: 'status-1', name: 'Active', category: 1, alias: 0 },
  isReleasable: false,
  tags: [],
} as unknown as ProductDto

const link = (
  id: string,
  to: { id: string; key: number; name: string },
  strength: DependencyStrength,
): ProductDependencyDto => ({
  id,
  product: self,
  dependsOnProduct: to,
  strength,
  startsOn: new Date('2026-01-01'),
  productPath: [],
  dependsOnProductPath: [],
})

const renderOverview = (dependencies: ProductDependenciesDto) =>
  render(
    <ProductOverview
      product={product}
      childProducts={[]}
      childProductsLoading={false}
      onNavigateToSection={jest.fn()}
      productsSectionId="products"
      dependencies={dependencies}
      dependenciesSectionId="dependencies"
    />,
  )

const getStored = jest.mocked(window.sessionStorage.getItem)
const setStored = jest.mocked(window.sessionStorage.setItem)

describe('ProductOverview', () => {
  afterEach(() => {
    getStored.mockReset()
    setStored.mockReset()
  })

  it('draws every link until the filter is changed', async () => {
    // Arrange
    renderOverview({
      dependsOn: [
        link(
          'hard',
          { id: 'p2', key: 2, name: 'Identity' },
          DependencyStrength.Hard,
        ),
        link(
          'soft',
          { id: 'p3', key: 3, name: 'Analytics' },
          DependencyStrength.Soft,
        ),
      ],
      usedBy: [],
    })

    // Act
    const edges = await screen.findByTestId('map-edges')

    // Assert
    expect(edges).toHaveTextContent('hard,soft')
    expect(
      screen.getByText(/A solid line is a hard dependency/),
    ).toBeInTheDocument()
  })

  it('draws only hard links, and remembers the choice for the session', async () => {
    // Arrange
    const user = userEvent.setup()
    renderOverview({
      dependsOn: [
        link(
          'hard',
          { id: 'p2', key: 2, name: 'Identity' },
          DependencyStrength.Hard,
        ),
        link(
          'soft',
          { id: 'p3', key: 3, name: 'Analytics' },
          DependencyStrength.Soft,
        ),
      ],
      usedBy: [],
    })

    // Act
    await user.click(await screen.findByText('Hard only'))

    // Assert
    expect(screen.getByTestId('map-edges')).toHaveTextContent(/^hard$/)
    expect(
      screen.getByText(/Showing hard dependencies only/),
    ).toBeInTheDocument()
    expect(setStored).toHaveBeenCalledWith(
      DEPENDENCY_STRENGTH_FILTER_KEY,
      'hard',
    )
  })

  it('opens filtered when the session already chose hard only', async () => {
    // Arrange
    getStored.mockReturnValue('hard')

    // Act
    renderOverview({
      dependsOn: [
        link(
          'hard',
          { id: 'p2', key: 2, name: 'Identity' },
          DependencyStrength.Hard,
        ),
        link(
          'soft',
          { id: 'p3', key: 3, name: 'Analytics' },
          DependencyStrength.Soft,
        ),
      ],
      usedBy: [],
    })

    // Assert
    expect(await screen.findByTestId('map-edges')).toHaveTextContent(/^hard$/)
  })

  it('keeps the map, and its filter, when every link is soft', async () => {
    // Arrange
    getStored.mockReturnValue('hard')

    // Act
    renderOverview({
      dependsOn: [
        link(
          'soft',
          { id: 'p3', key: 3, name: 'Analytics' },
          DependencyStrength.Soft,
        ),
      ],
      usedBy: [],
    })

    // Assert
    // Hiding the card here would take away the only way to turn the filter back off.
    expect(
      await screen.findByText('No hard dependencies in either direction.'),
    ).toBeInTheDocument()
    expect(screen.getByText('All')).toBeInTheDocument()
  })

  it('shows everything when session storage cannot be read', async () => {
    // Arrange
    getStored.mockImplementation(() => {
      throw new Error('SecurityError')
    })

    // Act
    renderOverview({
      dependsOn: [
        link(
          'soft',
          { id: 'p3', key: 3, name: 'Analytics' },
          DependencyStrength.Soft,
        ),
      ],
      usedBy: [],
    })

    // Assert
    expect(await screen.findByTestId('map-edges')).toHaveTextContent('soft')
  })

  it('says the overflow count is of hard links when filtered', async () => {
    // Arrange
    getStored.mockReturnValue('hard')

    // Act
    renderOverview({
      dependsOn: Array.from({ length: 8 }, (_, i) =>
        link(
          `h${i}`,
          { id: `p${i}`, key: 100 + i, name: `Provider ${i}` },
          DependencyStrength.Hard,
        ),
      ),
      usedBy: [],
    })

    // Assert
    // The link opens the grid, which is not filtered, so the count must say what it is counting.
    expect(await screen.findByText('+2 more hard')).toBeInTheDocument()
  })
})
