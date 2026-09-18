import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  DependencyStrength,
  NavigationDto,
  ProductDependenciesDto,
  ProductDependencyDto,
  ProductDto,
} from '@/src/services/wayd-api'
import type {
  DependencyNode,
  DependencyNodeData,
  DependencyOverflowData,
} from '../../../_components/dependency-map/dependency-neighbourhood'
import { expansionStorageKey } from '../../../_components/dependency-map/use-dependency-map-expansions'
import { DEPENDENCY_STRENGTH_FILTER_KEY } from '../../../_components/dependency-map/use-dependency-strength-filter'
import { useExpandedProductDependencies } from '../../../_components/dependency-map/use-expanded-product-dependencies'
import ProductDependencyMapCard from './product-dependency-map-card'

// The canvas measures itself and reads the theme provider, neither of which this test supplies. The stub
// renders what the card hands it and exposes the map's buttons; the map's own tests cover drawing.
jest.mock('../../../_components/dependency-map/dependency-map', () => ({
  __esModule: true,
  default: ({
    nodes,
    edges,
    filters,
    emptyText,
    onToggleExpansion,
    onShowAll,
  }: {
    nodes: DependencyNode[]
    edges: { id: string }[]
    filters?: React.ReactNode
    emptyText?: React.ReactNode
    onToggleExpansion?: (side: string, productId: string) => void
    onShowAll?: (side: string, ownerId: string | null) => void
  }) => (
    <div data-testid="dependency-map">
      {filters}
      <span data-testid="map-edges">{edges.map((e) => e.id).join(',')}</span>
      {nodes.map((node) => {
        if (node.type === 'overflow') {
          const overflow = node.data as DependencyOverflowData
          return (
            <button
              key={node.id}
              onClick={() => onShowAll?.(overflow.side, overflow.ownerId)}
            >
              {overflow.label}
            </button>
          )
        }
        const product = node.data as DependencyNodeData
        if (node.type !== 'product' || !product.expansion) return null
        return (
          <button
            key={node.id}
            data-status={product.expansion}
            onClick={() => onToggleExpansion?.(product.side, product.productId)}
          >
            {`Toggle ${product.label}`}
          </button>
        )
      })}
      {nodes.length === 0 && emptyText}
    </div>
  ),
}))

// Subscribing to the store is the hook's own business; here only what it returns matters.
jest.mock(
  '../../../_components/dependency-map/use-expanded-product-dependencies',
  () => ({ useExpandedProductDependencies: jest.fn() }),
)

const self: NavigationDto = { id: 'product-1', key: 7, name: 'Storefront' }
const identity: NavigationDto = { id: 'p2', key: 2, name: 'Identity' }
const analytics: NavigationDto = { id: 'p3', key: 3, name: 'Analytics' }
const directory: NavigationDto = { id: 'p4', key: 4, name: 'Directory' }
const ldap: NavigationDto = { id: 'p5', key: 5, name: 'LDAP' }

const product = {
  ...self,
  type: { id: 'type-1', key: 1, name: 'Application' },
  status: { id: 'status-1', name: 'Active', category: 1, alias: 0 },
  isReleasable: false,
  tags: [],
} as unknown as ProductDto

const link = (
  id: string,
  from: NavigationDto,
  to: NavigationDto,
  strength = DependencyStrength.Hard,
): ProductDependencyDto => ({
  id,
  product: from,
  dependsOnProduct: to,
  strength,
  startsOn: new Date('2026-01-01'),
  productPath: [],
  dependsOnProductPath: [],
})

const dependsOn = (
  ...links: ProductDependencyDto[]
): ProductDependenciesDto => ({
  dependsOn: links,
  usedBy: [],
})

const renderCard = (dependencies: ProductDependenciesDto) =>
  render(
    <ProductDependencyMapCard
      product={product}
      dependencies={dependencies}
      onViewAll={jest.fn()}
    />,
  )

// An in-memory session, so what the card writes is what it reads back.
let session: Record<string, string>
const getStored = jest.mocked(window.sessionStorage.getItem)
const setStored = jest.mocked(window.sessionStorage.setItem)
const expanded = jest.mocked(useExpandedProductDependencies)

const storedExpansions = () =>
  JSON.parse(session[expansionStorageKey(self.id)] ?? 'null')

describe('ProductDependencyMapCard', () => {
  beforeEach(() => {
    session = {}
    getStored.mockImplementation((key: string) => session[key] ?? null)
    setStored.mockImplementation((key: string, value: string) => {
      session[key] = value
    })
    expanded.mockReturnValue({})
  })

  afterEach(() => {
    getStored.mockReset()
    setStored.mockReset()
    expanded.mockReset()
  })

  it('is absent when the product has no dependencies', () => {
    // Act
    renderCard({ dependsOn: [], usedBy: [] })

    // Assert
    expect(screen.queryByTestId('dependency-map')).not.toBeInTheDocument()
  })

  it('draws every link until the filter is changed', async () => {
    // Arrange
    renderCard(
      dependsOn(
        link('hard', self, identity),
        link('soft', self, analytics, DependencyStrength.Soft),
      ),
    )

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
    renderCard(
      dependsOn(
        link('hard', self, identity),
        link('soft', self, analytics, DependencyStrength.Soft),
      ),
    )

    // Act
    await user.click(await screen.findByText('Hard only'))

    // Assert
    expect(screen.getByTestId('map-edges')).toHaveTextContent(/^hard$/)
    expect(
      screen.getByText(/Showing hard dependencies only/),
    ).toBeInTheDocument()
    expect(session[DEPENDENCY_STRENGTH_FILTER_KEY]).toBe('hard')
  })

  it('opens filtered when the session already chose hard only', async () => {
    // Arrange
    session[DEPENDENCY_STRENGTH_FILTER_KEY] = 'hard'

    // Act
    renderCard(
      dependsOn(
        link('hard', self, identity),
        link('soft', self, analytics, DependencyStrength.Soft),
      ),
    )

    // Assert
    expect(await screen.findByTestId('map-edges')).toHaveTextContent(/^hard$/)
  })

  it('keeps the map, and its filter, when every link is soft', async () => {
    // Arrange
    session[DEPENDENCY_STRENGTH_FILTER_KEY] = 'hard'

    // Act
    renderCard(
      dependsOn(link('soft', self, analytics, DependencyStrength.Soft)),
    )

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
    renderCard(
      dependsOn(link('soft', self, analytics, DependencyStrength.Soft)),
    )

    // Assert
    expect(await screen.findByTestId('map-edges')).toHaveTextContent('soft')
  })

  it("draws the rest of the subject's column in place, and remembers it", async () => {
    // Arrange
    const user = userEvent.setup()
    renderCard(
      dependsOn(
        ...Array.from({ length: 8 }, (_, i) =>
          link(`h${i}`, self, { id: `x${i}`, key: 100 + i, name: `P ${i}` }),
        ),
      ),
    )

    // Act
    await user.click(await screen.findByRole('button', { name: '+2 more' }))

    // Assert
    expect(
      screen.getByTestId('map-edges').textContent!.split(','),
    ).toHaveLength(8)
    expect(storedExpansions().subjectShowAll).toEqual(['dependsOn'])
  })

  it('expands a product outward and keeps the expansion for the session', async () => {
    // Arrange
    const user = userEvent.setup()
    expanded.mockImplementation((ids) =>
      Object.fromEntries(
        ids.map((id) => [
          id,
          { dependencies: dependsOn(link('beyond', identity, directory)) },
        ]),
      ),
    )
    renderCard(dependsOn(link('a', self, identity)))

    // Act
    await user.click(
      await screen.findByRole('button', { name: 'Toggle Identity' }),
    )

    // Assert
    expect(expanded).toHaveBeenLastCalledWith([identity.id])
    expect(screen.getByTestId('map-edges')).toHaveTextContent('a,beyond')
    expect(storedExpansions().dependsOn).toEqual({ [identity.id]: {} })
  })

  it('reopens with the expansions this product was left with', async () => {
    // Arrange
    session[expansionStorageKey(self.id)] = JSON.stringify({
      usedBy: {},
      dependsOn: { [identity.id]: {} },
      subjectShowAll: [],
    })

    // Act
    renderCard(dependsOn(link('a', self, identity)))

    // Assert
    await screen.findByTestId('dependency-map')
    expect(expanded).toHaveBeenLastCalledWith([identity.id])
  })

  it('collapses everything an expansion brought in, nested expansions included', async () => {
    // Arrange — Identity was expanded to Directory, and Directory to LDAP.
    const user = userEvent.setup()
    session[expansionStorageKey(self.id)] = JSON.stringify({
      usedBy: {},
      dependsOn: { [identity.id]: {}, [directory.id]: {} },
      subjectShowAll: [],
    })
    const responses: Record<string, ProductDependenciesDto> = {
      [identity.id]: dependsOn(link('b', identity, directory)),
      [directory.id]: dependsOn(link('c', directory, ldap)),
    }
    expanded.mockImplementation((ids) =>
      Object.fromEntries(
        ids.map((id) => [id, { dependencies: responses[id] }]),
      ),
    )
    renderCard(dependsOn(link('a', self, identity)))
    expect(await screen.findByTestId('map-edges')).toHaveTextContent('a,b,c')

    // Act
    await user.click(screen.getByRole('button', { name: 'Toggle Identity' }))

    // Assert
    // Directory is no longer on the map, so its expansion goes too; expanding Identity again starts clean.
    expect(screen.getByTestId('map-edges')).toHaveTextContent(/^a$/)
    expect(storedExpansions().dependsOn).toEqual({})
  })

  it('shows the rest of an expansion in place', async () => {
    // Arrange
    const user = userEvent.setup()
    session[expansionStorageKey(self.id)] = JSON.stringify({
      usedBy: {},
      dependsOn: { [identity.id]: {} },
      subjectShowAll: [],
    })
    expanded.mockReturnValue({
      [identity.id]: {
        dependencies: dependsOn(
          ...Array.from({ length: 7 }, (_, i) =>
            link(`x${i}`, identity, {
              id: `y${i}`,
              key: 200 + i,
              name: `Q ${i}`,
            }),
          ),
        ),
      },
    })
    renderCard(dependsOn(link('a', self, identity)))

    // Act
    await user.click(
      await screen.findByRole('button', { name: '+1 more for Identity' }),
    )

    // Assert
    const map = screen.getByTestId('dependency-map')
    expect(
      within(map).getByTestId('map-edges').textContent!.split(','),
    ).toHaveLength(8)
    expect(storedExpansions().dependsOn[identity.id]).toEqual({ showAll: true })
  })
})
