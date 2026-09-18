import {
  DependencyStrength,
  NavigationDto,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import {
  buildDependencyNeighbourhood,
  groupNodeId,
  sideNodeId,
  type DependencyNode,
  type DependencyNodeSide,
} from './dependency-neighbourhood'

const PRODUCT_ID = '11111111-1111-1111-1111-111111111111'

const nav = (id: string, name: string, key: number): NavigationDto => ({
  id,
  name,
  key,
})

const link = (
  id: string,
  from: NavigationDto,
  to: NavigationDto,
  overrides: Partial<ProductDependencyDto> = {},
): ProductDependencyDto => ({
  id,
  product: from,
  dependsOnProduct: to,
  strength: DependencyStrength.Hard,
  startsOn: new Date('2026-01-01'),
  productPath: [],
  dependsOnProductPath: [],
  ...overrides,
})

const dependencies = (
  dependsOn: ProductDependencyDto[],
  usedBy: ProductDependencyDto[] = [],
): ProductDependenciesDto => ({ dependsOn, usedBy })

const build = (deps: ProductDependenciesDto | undefined, maxPerSide?: number) =>
  buildDependencyNeighbourhood({
    productId: PRODUCT_ID,
    productName: 'Storefront',
    productKey: 7,
    dependencies: deps,
    maxPerSide,
  })

/** What a box holds, in the order it holds it. */
const inside = (
  nodes: DependencyNode[],
  productId: string,
  side: DependencyNodeSide = 'center',
) =>
  nodes
    .filter((node) => node.parentId === groupNodeId(side, productId))
    .map((node) => node.id)

describe('buildDependencyNeighbourhood', () => {
  it('centres the product and puts each side in its own column', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const identity = nav('22', 'Identity Service', 12)
    const kiosk = nav('33', 'Storefront Kiosk', 14)

    // Act
    const graph = build(
      dependencies([link('a', self, identity)], [link('b', kiosk, self)]),
    )

    // Assert
    const byId = Object.fromEntries(graph.nodes.map((n) => [n.id, n]))
    expect(byId[PRODUCT_ID].data.side).toBe('center')
    expect(byId['dependsOn:22'].data.side).toBe('dependsOn')
    expect(byId['usedBy:33'].data.side).toBe('usedBy')
    expect(byId['usedBy:33'].position.x).toBeLessThan(
      byId[PRODUCT_ID].position.x,
    )
    expect(byId['dependsOn:22'].position.x).toBeGreaterThan(
      byId[PRODUCT_ID].position.x,
    )
  })

  it('points every edge from the dependent product to the one it depends on', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const identity = nav('22', 'Identity Service', 12)
    const kiosk = nav('33', 'Storefront Kiosk', 14)

    // Act
    const graph = build(
      dependencies([link('a', self, identity)], [link('b', kiosk, self)]),
    )

    // Assert
    // Direction is the whole meaning of the map: reversing one would say the provider needs the consumer.
    expect(graph.edges).toEqual([
      expect.objectContaining({
        id: 'a',
        source: PRODUCT_ID,
        target: 'dependsOn:22',
      }),
      expect.objectContaining({
        id: 'b',
        source: 'usedBy:33',
        target: PRODUCT_ID,
      }),
    ])
  })

  it('leaves ended links off whatever the caller passed', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
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
    // Arrange — two of the product's services depend on the same provider.
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const web = nav('44', 'Storefront Web', 8)
    const mobile = nav('55', 'Storefront Mobile', 9)
    const identity = nav('22', 'Identity Service', 12)

    // Act
    const graph = build(
      dependencies([
        link('a', web, identity, { productPath: [self] }),
        link('b', mobile, identity, { productPath: [self] }),
      ]),
    )

    // Assert
    expect(graph.nodes.filter((n) => n.id === 'dependsOn:22')).toHaveLength(1)
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['44', 'dependsOn:22'],
      ['55', 'dependsOn:22'],
    ])
  })

  it('nests a descendant under everything it sits inside', () => {
    // Arrange — the link is held two levels down, by a service of one of the product's own products.
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const apps = nav('40', 'Storefront Apps', 8)
    const web = nav('44', 'Storefront Web', 9)
    const identity = nav('22', 'Identity Service', 12)

    // Act
    const graph = build(
      dependencies([link('a', web, identity, { productPath: [self, apps] })]),
    )

    // Assert
    // Flattened, the map would show Storefront Web directly inside Storefront and lose the product it
    // actually belongs to.
    expect(inside(graph.nodes, PRODUCT_ID)).toEqual([
      groupNodeId('center', '40'),
    ])
    expect(inside(graph.nodes, '40')).toEqual(['44'])
    expect(graph.edges[0].source).toBe('44')
  })

  it('draws the product itself inside its box when it holds a link of its own', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const web = nav('44', 'Storefront Web', 9)

    // Act
    const graph = build(
      dependencies([
        link('a', self, nav('22', 'Identity Service', 12)),
        link('b', web, nav('23', 'Mapping API', 13), { productPath: [self] }),
      ]),
    )

    // Assert
    // Its own node first, then what it contains, so the subject does not move as descendants are added.
    expect(inside(graph.nodes, PRODUCT_ID)).toEqual([PRODUCT_ID, '44'])
  })

  it('nests the far end under whatever owns it', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const platform = nav('20', 'Core Platform', 30)
    const identity = nav('22', 'Identity Service', 12)

    // Act
    const graph = build(
      dependencies([
        link('a', self, identity, { dependsOnProductPath: [platform] }),
      ]),
    )

    // Assert
    // A row carrying only "Identity Service" cannot say whose service it is; the box does.
    expect(inside(graph.nodes, '20', 'dependsOn')).toEqual(['dependsOn:22'])
    expect(graph.edges[0].target).toBe('dependsOn:22')
  })

  it('draws one box for two services of the same platform', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const platform = nav('20', 'Core Platform', 30)

    // Act
    const graph = build(
      dependencies([
        link('a', self, nav('22', 'Identity Service', 12), {
          dependsOnProductPath: [platform],
        }),
        link('b', self, nav('23', 'Search Service', 13), {
          dependsOnProductPath: [platform],
        }),
      ]),
    )

    // Assert
    expect(
      graph.nodes.filter((n) => n.id === groupNodeId('dependsOn', '20')),
    ).toHaveLength(1)
    expect(inside(graph.nodes, '20', 'dependsOn')).toEqual([
      'dependsOn:22',
      'dependsOn:23',
    ])
  })

  it('sizes a box to hold everything inside it', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const web = nav('44', 'Storefront Web', 9)
    const mobile = nav('55', 'Storefront Mobile', 10)

    // Act
    const graph = build(
      dependencies([
        link('a', web, nav('22', 'Identity Service', 12), {
          productPath: [self],
        }),
        link('b', mobile, nav('23', 'Mapping API', 13), {
          productPath: [self],
        }),
      ]),
    )

    // Assert
    // The image export sizes the box from these, so a box that fits on screen but not in the data would
    // export with its children spilling out.
    const box = graph.nodes.find(
      (n) => n.id === groupNodeId('center', PRODUCT_ID),
    )!
    const members = graph.nodes.filter((n) => n.parentId === box.id)
    const lowest = Math.max(...members.map((n) => n.position.y))

    expect(box.width).toBeGreaterThan(0)
    expect(box.height).toBeGreaterThan(lowest)
  })

  it('lists a box before the nodes inside it', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const web = nav('44', 'Storefront Web', 9)

    // Act
    const graph = build(
      dependencies([
        link('a', web, nav('22', 'Identity Service', 12), {
          productPath: [self],
        }),
      ]),
    )

    // Assert
    // React Flow drops a node whose parent it has not seen yet, which loses the node silently.
    const box = graph.nodes.findIndex(
      (n) => n.id === groupNodeId('center', PRODUCT_ID),
    )
    const member = graph.nodes.findIndex((n) => n.id === '44')
    expect(box).toBeLessThan(member)
  })

  it('never draws the product inside its own parents', () => {
    // Arrange — a service two levels down the catalog, holding every link itself.
    const cloud = nav('10', 'Trust & Safety Cloud', 1)
    const reporting = nav('11', 'Trust & Safety Reporting', 2)
    const self = nav(PRODUCT_ID, 'Accounts API', 7)

    // Act
    const graph = build(
      dependencies(
        [
          link('a', self, nav('22', 'Data Platform Service', 12), {
            productPath: [cloud, reporting],
          }),
        ],
        [
          link('b', nav('33', 'Billing Portal', 14), self, {
            dependsOnProductPath: [cloud, reporting],
          }),
        ],
      ),
    )

    // Assert
    // The product is not in its own path, so an untrimmed chain built its parents inside its box —
    // reading as though Trust & Safety Reporting were part of Accounts API rather than the reverse.
    expect(graph.nodes.filter((n) => n.type === 'productGroup')).toHaveLength(0)
    expect(graph.nodes.map((n) => n.data.label)).not.toContain(
      'Trust & Safety Reporting',
    )
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      [PRODUCT_ID, 'dependsOn:22'],
      ['usedBy:33', PRODUCT_ID],
    ])
  })

  it('draws no box when nothing is nested', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)

    // Act
    const graph = build(
      dependencies([link('a', self, nav('22', 'Identity Service', 12))]),
    )

    // Assert
    // A box around the subject alone adds a frame that says nothing.
    expect(graph.nodes.some((n) => n.type === 'productGroup')).toBe(false)
    expect(graph.hasContainedProducts).toBe(false)
  })

  it('draws a product on both sides of a cycle in each column', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const gateway = nav('22', 'Gateway Service', 12)

    // Act
    const graph = build(
      dependencies([link('a', self, gateway)], [link('b', gateway, self)]),
    )

    // Assert
    // One node would have to sit in one column, leaving an arrow pointing back the way it came as the
    // only sign that the product is on the wrong side — and would drag its whole box across with it.
    expect(graph.nodes.map((n) => n.id)).toEqual(
      expect.arrayContaining([
        sideNodeId('dependsOn', '22'),
        sideNodeId('usedBy', '22'),
      ]),
    )
    expect(graph.edges).toHaveLength(2)
  })

  it('caps each side by product and counts what it left off', () => {
    // Arrange
    const self = nav(PRODUCT_ID, 'Storefront', 7)
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
    const self = nav(PRODUCT_ID, 'Storefront', 7)
    const shared = nav('99', 'Shared Provider', 30)

    // Act
    const graph = build(
      dependencies([
        link('a', self, nav('p1', 'One', 1)),
        link('b', self, shared),
        link('c', nav('44', 'Storefront Sync', 9), shared, {
          productPath: [self],
        }),
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
    const self = nav(PRODUCT_ID, 'Storefront', 7)
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
    expect(one.height).toBeGreaterThanOrEqual(240)
    expect(many.height).toBeLessThanOrEqual(720)
  })
})
