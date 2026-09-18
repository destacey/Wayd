import {
  DependencyStrength,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { buildDependencyNeighbourhood } from './dependency-neighbourhood'

const PRODUCT_ID = '11111111-1111-1111-1111-111111111111'

const nav = (id: string, name: string, key: number) => ({ id, name, key })

const link = (
  id: string,
  from: { id: string; name: string; key: number },
  to: { id: string; name: string; key: number },
  overrides: Partial<ProductDependencyDto> = {},
): ProductDependencyDto => ({
  id,
  product: from,
  dependsOnProduct: to,
  strength: DependencyStrength.Hard,
  startsOn: new Date('2026-01-01'),
  ...overrides,
})

const dependencies = (
  dependsOn: ProductDependencyDto[],
  usedBy: ProductDependencyDto[] = [],
): ProductDependenciesDto => ({ dependsOn, usedBy })

const build = (deps: ProductDependenciesDto | undefined, maxPerSide?: number) =>
  buildDependencyNeighbourhood({
    productId: PRODUCT_ID,
    productName: 'Trio VMS',
    productKey: 7,
    dependencies: deps,
    maxPerSide,
  })

describe('buildDependencyNeighbourhood', () => {
  it('centres the product and puts each side in its own column', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const identity = nav('22', 'Argo Identity', 12)
    const kiosk = nav('33', 'Trio Kiosk', 14)

    // Act
    const graph = build(
      dependencies([link('a', self, identity)], [link('b', kiosk, self)]),
    )

    // Assert
    const byId = Object.fromEntries(graph.nodes.map((n) => [n.id, n]))
    expect(byId[PRODUCT_ID].data.side).toBe('center')
    expect(byId['22'].data.side).toBe('dependsOn')
    expect(byId['33'].data.side).toBe('usedBy')
    expect(byId['33'].position.x).toBeLessThan(byId[PRODUCT_ID].position.x)
    expect(byId['22'].position.x).toBeGreaterThan(byId[PRODUCT_ID].position.x)
  })

  it('points every edge from the dependent product to the one it depends on', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const identity = nav('22', 'Argo Identity', 12)
    const kiosk = nav('33', 'Trio Kiosk', 14)

    // Act
    const graph = build(
      dependencies([link('a', self, identity)], [link('b', kiosk, self)]),
    )

    // Assert
    // Direction is the whole meaning of the map: reversing one would say the provider needs the consumer.
    expect(graph.edges).toEqual([
      expect.objectContaining({ id: 'a', source: PRODUCT_ID, target: '22' }),
      expect.objectContaining({ id: 'b', source: '33', target: PRODUCT_ID }),
    ])
  })

  it('leaves ended links off whatever the caller passed', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const retired = nav('22', 'Legacy Auth', 12)

    // Act
    // The Dependencies section's Show ended switch feeds the same query, so the map must filter for itself.
    const graph = build(
      dependencies([
        link('a', self, retired, { endsOn: new Date('2026-03-01') }),
      ]),
    )

    // Assert
    expect(graph.nodes).toEqual([])
    expect(graph.edges).toEqual([])
  })

  it('draws one node per product however many links reach it', () => {
    // Arrange — a rolled-up parent: two of its services depend on the same provider.
    const vms = nav('44', 'Trio VMS', 7)
    const shifts = nav('55', 'Trio Shifts', 8)
    const identity = nav('22', 'Argo Identity', 12)

    // Act
    const graph = build(
      dependencies([link('a', vms, identity), link('b', shifts, identity)]),
    )

    // Assert
    expect(graph.nodes.filter((n) => n.id === '22')).toHaveLength(1)
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['44', '22'],
      ['55', '22'],
    ])
  })

  it('draws a descendant that holds a link as its own node inside the box', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const descendant = nav('44', 'VMS Sync', 9)
    const identity = nav('22', 'Argo Identity', 12)

    // Act
    const graph = build(
      dependencies([
        link('a', self, identity),
        link('b', descendant, identity),
      ]),
    )

    // Assert
    // Crediting the parent with its child's link would claim a dependency the parent does not hold.
    const group = graph.nodes.find((n) => n.type === 'productGroup')
    expect(group).toBeDefined()
    expect(
      graph.nodes.filter((n) => n.parentId === group!.id).map((n) => n.id),
    ).toEqual([PRODUCT_ID, '44'])
    expect(graph.edges.map((e) => e.source)).toEqual([PRODUCT_ID, '44'])
  })

  it('leaves the subject out of the box when every link is its descendants', () => {
    // Arrange — a platform that holds no links of its own.
    const vms = nav('44', 'Trio VMS', 7)
    const shifts = nav('55', 'Trio Shifts', 8)

    // Act
    const graph = build(
      dependencies([
        link('a', vms, nav('22', 'Argo Identity', 12)),
        link('b', shifts, nav('23', 'Mapping API', 13)),
      ]),
    )

    // Assert
    // Drawing it would show a node with no edges, which reads as a product that depends on nothing.
    // Shifts before VMS: descendants are ordered by name, so the box does not reshuffle as links change.
    const group = graph.nodes.find((n) => n.type === 'productGroup')!
    expect(
      graph.nodes.filter((n) => n.parentId === group.id).map((n) => n.id),
    ).toEqual(['55', '44'])
    expect(graph.nodes.some((n) => n.id === PRODUCT_ID)).toBe(false)
  })

  it('draws no box when nothing is rolled up', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)

    // Act
    const graph = build(
      dependencies([link('a', self, nav('22', 'Argo Identity', 12))]),
    )

    // Assert
    // A box around the subject alone adds a frame that says nothing.
    expect(graph.nodes.some((n) => n.type === 'productGroup')).toBe(false)
    expect(graph.nodes[0].id).toBe(PRODUCT_ID)
  })

  it('lists the box before the nodes inside it', () => {
    // Arrange
    const descendant = nav('44', 'VMS Sync', 9)

    // Act
    const graph = build(
      dependencies([link('a', descendant, nav('22', 'Argo Identity', 12))]),
    )

    // Assert
    // React Flow drops a node whose parent it has not seen yet, which loses the node silently.
    expect(graph.nodes[0].type).toBe('productGroup')
    expect(graph.nodes[1].parentId).toBe(graph.nodes[0].id)
  })

  it('draws a cycle as one node with an edge each way', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const gateway = nav('22', 'Argo Gateway', 12)

    // Act
    const graph = build(
      dependencies([link('a', self, gateway)], [link('b', gateway, self)]),
    )

    // Assert
    expect(graph.nodes.filter((n) => n.id === '22')).toHaveLength(1)
    expect(graph.edges).toHaveLength(2)
  })

  it('caps each side by product and counts what it left off', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const links = Array.from({ length: 5 }, (_, i) =>
      link(`a${i}`, self, nav(`p${i}`, `Provider ${i}`, 100 + i)),
    )

    // Act
    const graph = build(dependencies(links), 3)

    // Assert
    expect(graph.nodes.filter((n) => n.data.side === 'dependsOn')).toHaveLength(
      3,
    )
    expect(graph.hiddenCount).toBe(2)
  })

  it('keeps a capped product whole rather than cutting its links', () => {
    // Arrange — the third product has two links; either both are drawn or neither is.
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const shared = nav('99', 'Shared Provider', 30)

    // Act
    const graph = build(
      dependencies([
        link('a', self, nav('p1', 'One', 1)),
        link('b', self, shared),
        link('c', nav('44', 'VMS Sync', 9), shared),
      ]),
      2,
    )

    // Assert
    // A node showing one of its two edges would misstate which parts of the product rely on it.
    expect(graph.edges.map((e) => e.id)).toEqual(['a', 'b', 'c'])
    expect(graph.hiddenCount).toBe(0)
  })

  it('returns an empty graph when the product has no dependencies', () => {
    // Act
    const graph = build(dependencies([], []))

    // Assert
    // The centre node is added only when there is something to connect it to — a lone node is not a map.
    expect(graph.nodes).toEqual([])
  })

  it('returns an empty graph before the dependencies have loaded', () => {
    // Act
    const graph = build(undefined)

    // Assert
    expect(graph).toEqual({
      nodes: [],
      edges: [],
      hiddenCount: 0,
      height: 0,
      hasContainedProducts: false,
    })
  })

  it('asks for a canvas that grows with the graph, within bounds', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Trio VMS', 7)
    const one = build(dependencies([link('a', self, nav('22', 'One', 1))]))

    // Act
    const many = build(
      dependencies(
        Array.from({ length: 6 }, (_, i) =>
          link(`a${i}`, self, nav(`p${i}`, `Provider ${i}`, 100 + i)),
        ),
      ),
    )

    // Assert
    // A fixed height leaves a single link sitting in an empty field, and buries a busy one.
    expect(one.height).toBeLessThan(many.height)
    expect(one.height).toBeGreaterThanOrEqual(180)
    expect(many.height).toBeLessThanOrEqual(420)
  })
})
