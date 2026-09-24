import {
  DependencyStrength,
  NavigationDto,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import {
  DEPENDENCY_HANDLES,
  groupNodeId,
  overflowNodeId,
  sideNodeId,
  type DependencyNode,
  type DependencyNodeData,
  type DependencyNodeSide,
} from '@/src/components/common/dependency-map/dependency-neighbourhood'
import {
  buildProductDependencyNeighbourhood,
  type BuildProductDependencyNeighbourhoodOptions,
  type ProductDependencyExpansions,
} from './product-dependency-neighbourhood'

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
  buildProductDependencyNeighbourhood({
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

describe('buildProductDependencyNeighbourhood', () => {
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
    expect(byId['right:22'].data.side).toBe('right')
    expect(byId['left:33'].data.side).toBe('left')
    expect(byId['left:33'].position.x).toBeLessThan(byId[PRODUCT_ID].position.x)
    expect(byId['right:22'].position.x).toBeGreaterThan(
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
        target: 'right:22',
      }),
      expect.objectContaining({
        id: 'b',
        source: 'left:33',
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
    expect(graph.nodes.filter((n) => n.id === 'right:22')).toHaveLength(1)
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['44', 'right:22'],
      ['55', 'right:22'],
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
    expect(inside(graph.nodes, '20', 'right')).toEqual(['right:22'])
    expect(graph.edges[0].target).toBe('right:22')
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
      graph.nodes.filter((n) => n.id === groupNodeId('right', '20')),
    ).toHaveLength(1)
    expect(inside(graph.nodes, '20', 'right')).toEqual(['right:22', 'right:23'])
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
    expect(graph.nodes.filter((n) => n.type === 'recordGroup')).toHaveLength(0)
    expect(graph.nodes.map((n) => n.data.label)).not.toContain(
      'Trust & Safety Reporting',
    )
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      [PRODUCT_ID, 'right:22'],
      ['left:33', PRODUCT_ID],
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
    expect(graph.nodes.some((n) => n.type === 'recordGroup')).toBe(false)
    expect(graph.hasContainedRecords).toBe(false)
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
        sideNodeId('right', '22'),
        sideNodeId('left', '22'),
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
    expect(
      graph.nodes.filter((n) => n.type === 'record' && n.data.side === 'right'),
    ).toHaveLength(3)
    expect(graph.hiddenCount).toBe(2)
    expect(
      graph.nodes.find((n) => n.id === overflowNodeId('right', null))?.data,
    ).toEqual(expect.objectContaining({ label: '+2 more', count: 2 }))
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
      hasContainedRecords: false,
      placed: { left: [], right: [] },
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

describe('buildProductDependencyNeighbourhood with only hard links', () => {
  const self = nav(PRODUCT_ID, 'Storefront', 7)
  const soft = { strength: DependencyStrength.Soft }

  const buildHard = (deps: ProductDependenciesDto, maxPerSide?: number) =>
    buildProductDependencyNeighbourhood({
      productId: PRODUCT_ID,
      productName: 'Storefront',
      productKey: 7,
      dependencies: deps,
      maxPerSide,
      strengthFilter: 'hard',
    })

  it('drops a product whose only links are soft rather than leaving it unconnected', () => {
    // Arrange
    const identity = nav('22', 'Identity Service', 12)
    const analytics = nav('23', 'Analytics', 13)
    const kiosk = nav('33', 'Storefront Kiosk', 14)

    // Act
    const graph = buildHard(
      dependencies(
        [link('a', self, identity), link('b', self, analytics, soft)],
        [link('c', kiosk, self, soft)],
      ),
    )

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['a'])
    expect(graph.nodes.map((n) => n.id)).toEqual([PRODUCT_ID, 'right:22'])
  })

  it('drops a box once nothing inside it is left', () => {
    // Arrange — both of the platform's services are relied on only softly.
    const platform = nav('20', 'Core Platform', 30)
    const web = nav('44', 'Storefront Web', 9)

    // Act
    const graph = buildHard(
      dependencies([
        link('a', self, nav('22', 'Identity Service', 12), {
          ...soft,
          dependsOnProductPath: [platform],
        }),
        link('b', web, nav('23', 'Search Service', 13), {
          ...soft,
          productPath: [self],
          dependsOnProductPath: [platform],
        }),
        link('c', self, nav('24', 'Payments', 15)),
      ]),
    )

    // Assert
    // An empty box would read as a product the subject relies on, with no line to say how.
    expect(graph.nodes.map((n) => n.id)).toEqual([PRODUCT_ID, 'right:24'])
    expect(graph.hasContainedRecords).toBe(false)
  })

  it('keeps a box for the one service still relied on hard, without its soft sibling', () => {
    // Arrange
    const platform = nav('20', 'Core Platform', 30)

    // Act
    const graph = buildHard(
      dependencies([
        link('a', self, nav('22', 'Identity Service', 12), {
          dependsOnProductPath: [platform],
        }),
        link('b', self, nav('23', 'Search Service', 13), {
          ...soft,
          dependsOnProductPath: [platform],
        }),
      ]),
    )

    // Assert
    expect(inside(graph.nodes, '20', 'right')).toEqual(['right:22'])
  })

  it('caps after filtering, so soft links cannot crowd out hard ones', () => {
    // Arrange — the soft providers sort first, and would fill the cap if it were applied before the filter.
    const links = [
      link('s1', self, nav('p1', 'Alpha', 101), soft),
      link('s2', self, nav('p2', 'Beta', 102), soft),
      link('h1', self, nav('p3', 'Gamma', 103)),
      link('h2', self, nav('p4', 'Delta', 104)),
      link('h3', self, nav('p5', 'Epsilon', 105)),
    ]

    // Act
    const graph = buildHard(dependencies(links), 2)

    // Assert
    // The count is of hard links only, because that is what the map says it is showing.
    expect(graph.edges.map((e) => e.id)).toEqual(['h1', 'h2'])
    expect(graph.hiddenCount).toBe(1)
  })

  it('draws one side of a mutual pair when only one direction is hard', () => {
    // Arrange
    const billing = nav('22', 'Billing', 12)

    // Act
    const graph = buildHard(
      dependencies(
        [link('a', self, billing)],
        [link('b', billing, self, soft)],
      ),
    )

    // Assert
    expect(graph.nodes.map((n) => n.id)).toEqual([PRODUCT_ID, 'right:22'])
  })

  it('returns an empty graph when every link is soft', () => {
    // Act
    const graph = buildHard(
      dependencies([link('a', self, nav('22', 'Analytics', 12), soft)]),
    )

    // Assert
    expect(graph.nodes).toEqual([])
    expect(graph.edges).toEqual([])
  })
})

describe('buildProductDependencyNeighbourhood with expansions', () => {
  const self = nav(PRODUCT_ID, 'Storefront', 7)
  const identity = nav('22', 'Identity Service', 12)
  const kiosk = nav('33', 'Storefront Kiosk', 14)

  const noExpansions = (): ProductDependencyExpansions => ({
    left: {},
    right: {},
  })

  const buildWith = (
    deps: ProductDependenciesDto,
    expansions: ProductDependencyExpansions,
    options: Partial<BuildProductDependencyNeighbourhoodOptions> = {},
  ) =>
    buildProductDependencyNeighbourhood({
      productId: PRODUCT_ID,
      productName: 'Storefront',
      productKey: 7,
      dependencies: deps,
      expansions,
      ...options,
    })

  const node = (graph: { nodes: DependencyNode[] }, id: string) =>
    graph.nodes.find((n) => n.id === id)

  it('grows a provider outward, into a column beyond it', () => {
    // Arrange
    const directory = nav('40', 'Directory', 20)
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([link('x', identity, directory)]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
    )

    // Assert
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      [PRODUCT_ID, 'right:22'],
      ['right:22', 'right:40'],
    ])
    expect(node(graph, 'right:40')!.position.x).toBeGreaterThan(
      node(graph, 'right:22')!.position.x,
    )
  })

  it('grows a consumer outward, into a column before it', () => {
    // Arrange
    const signage = nav('50', 'Signage', 21)
    const expansions = noExpansions()
    expansions.left['33'] = {
      dependencies: dependencies([], [link('y', signage, kiosk)]),
    }

    // Act
    const graph = buildWith(
      dependencies([], [link('b', kiosk, self)]),
      expansions,
    )

    // Assert
    // What relies on a consumer: an outage reaches it through the consumer, so it sits further left.
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['left:33', PRODUCT_ID],
      ['left:50', 'left:33'],
    ])
    expect(node(graph, 'left:50')!.position.x).toBeLessThan(
      node(graph, 'left:33')!.position.x,
    )
  })

  it('never turns an expansion back toward the centre', () => {
    // Arrange — the provider's response also lists what relies on it.
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies(
        [],
        [link('z', nav('60', 'Admin Console', 30), identity)],
      ),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
    )

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['a'])
    expect(node(graph, 'right:22')!.data.expansion).toBe('empty')
  })

  it('draws only the links the expanded product holds itself', () => {
    // Arrange — the endpoint rolls a product's subtree up; a link held beneath it is not its own.
    const platform = nav('20', 'Core Platform', 30)
    const tokens = nav('24', 'Token Service', 31)
    const expansions = noExpansions()
    expansions.right['20'] = {
      dependencies: dependencies([
        link('own', platform, nav('70', 'Cloud Hosting', 40)),
        link('child', tokens, nav('71', 'Key Vault', 41), {
          productPath: [platform],
        }),
      ]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, platform)]),
      expansions,
    )

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['a', 'own'])
    expect(node(graph, 'right:71')).toBeUndefined()
  })

  it('leaves out links back into the subject, which the centre already draws', () => {
    // Arrange — the provider depends on something beneath the subject.
    const web = nav('44', 'Storefront Web', 9)
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([
        link('back', identity, web, { dependsOnProductPath: [self] }),
      ]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
    )

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['a'])
  })

  it('draws a product once per side, where it was first found', () => {
    // Arrange — the subject depends on both, and one of them depends on the other.
    const directory = nav('40', 'Directory', 20)
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([link('x', identity, directory)]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity), link('b', self, directory)]),
      expansions,
    )

    // Assert
    // The link between them is drawn within the column rather than moving Directory further out.
    expect(graph.nodes.filter((n) => n.id === 'right:40')).toHaveLength(1)
    expect(node(graph, 'right:40')!.position.x).toBe(
      node(graph, 'right:22')!.position.x,
    )
    expect(graph.edges.map((e) => e.id)).toEqual(['a', 'b', 'x'])
  })

  it('bows a link within a column out on its outer side', () => {
    // Arrange — the subject depends on both, and one of them depends on the other.
    const directory = nav('40', 'Directory', 20)
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([link('x', identity, directory)]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity), link('b', self, directory)]),
      expansions,
    )

    // Assert
    // Right to left would cut across the links arriving from the subject.
    expect(graph.edges.find((e) => e.id === 'x')).toEqual(
      expect.objectContaining({
        sourceHandle: DEPENDENCY_HANDLES.outRight,
        targetHandle: DEPENDENCY_HANDLES.inRight,
      }),
    )
  })

  it('runs a link back toward the subject from left side to right side', () => {
    // Arrange — Directory is two hops out, and relies on Identity, which the subject also relies on.
    const gateway = nav('30', 'Gateway', 19)
    const directory = nav('40', 'Directory', 20)
    const expansions = noExpansions()
    expansions.right['30'] = {
      dependencies: dependencies([link('x', gateway, directory)]),
    }
    expansions.right['40'] = {
      dependencies: dependencies([link('y', directory, identity)]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity), link('b', self, gateway)]),
      expansions,
    )

    // Assert
    // Leaving the right side would loop the curve across the whole map and through every box on the way.
    expect(graph.edges.find((e) => e.id === 'y')).toEqual(
      expect.objectContaining({
        source: 'right:40',
        target: 'right:22',
        sourceHandle: DEPENDENCY_HANDLES.outLeft,
        targetHandle: DEPENDENCY_HANDLES.inRight,
      }),
    )
    expect(graph.edges.find((e) => e.id === 'x')).toEqual(
      expect.objectContaining({
        sourceHandle: DEPENDENCY_HANDLES.outRight,
        targetHandle: DEPENDENCY_HANDLES.inLeft,
      }),
    )
  })

  it('bows a link within a consumer column out on the left', () => {
    // Arrange — two consumers of the subject, one of which uses the other.
    const signage = nav('50', 'Signage', 21)
    const expansions = noExpansions()
    expansions.left['33'] = {
      dependencies: dependencies([], [link('y', signage, kiosk)]),
    }

    // Act
    const graph = buildWith(
      dependencies([], [link('b', kiosk, self), link('c', signage, self)]),
      expansions,
    )

    // Assert
    expect(graph.edges.find((e) => e.id === 'y')).toEqual(
      expect.objectContaining({
        sourceHandle: DEPENDENCY_HANDLES.outLeft,
        targetHandle: DEPENDENCY_HANDLES.inLeft,
      }),
    )
  })

  it('draws a link once however many expansions reach it', () => {
    // Arrange — the two ends of one link, both expanded, both return it.
    const directory = nav('40', 'Directory', 20)
    const shared = link('x', identity, directory)
    const expansions = noExpansions()
    expansions.right['22'] = { dependencies: dependencies([shared]) }
    expansions.right['40'] = {
      dependencies: dependencies([], [shared]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
    )

    // Assert
    expect(graph.edges.filter((e) => e.id === 'x')).toHaveLength(1)
  })

  it('says where each product off to the side stands with its own links', () => {
    // Arrange
    const one = nav('81', 'One', 81)
    const two = nav('82', 'Two', 82)
    const three = nav('83', 'Three', 83)
    const four = nav('84', 'Four', 84)
    const expansions = noExpansions()
    expansions.right['82'] = {}
    expansions.right['83'] = { isError: true }
    expansions.right['84'] = {
      dependencies: dependencies([link('x', four, nav('90', 'Beyond', 90))]),
    }

    // Act
    const graph = buildWith(
      dependencies([
        link('a', self, one),
        link('b', self, two),
        link('c', self, three),
        link('d', self, four),
      ]),
      expansions,
    )

    // Assert
    const status = (id: string) =>
      (node(graph, `right:${id}`)!.data as DependencyNodeData).expansion
    expect(status('81')).toBe('collapsed')
    expect(status('82')).toBe('loading')
    expect(status('83')).toBe('error')
    expect(status('84')).toBe('expanded')
    expect(status('90')).toBe('collapsed')
    // The subject is not a neighbour, and cannot be expanded.
    expect(
      (node(graph, PRODUCT_ID)!.data as DependencyNodeData).expansion,
    ).toBeUndefined()
  })

  it('caps an expansion, and names whose products the count is of', () => {
    // Arrange
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies(
        Array.from({ length: 4 }, (_, i) =>
          link(`x${i}`, identity, nav(`p${i}`, `Provider ${i}`, 100 + i)),
        ),
      ),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
      { maxPerSide: 3 },
    )

    // Assert
    expect(node(graph, overflowNodeId('right', '22'))!.data).toEqual(
      expect.objectContaining({
        label: '+1 more for Identity Service',
        ownerId: '22',
        count: 1,
      }),
    )
    // Only the subject's own overflow is the card's to count.
    expect(graph.hiddenCount).toBe(0)
  })

  it('draws the rest of an expansion once asked', () => {
    // Arrange
    const expansions = noExpansions()
    expansions.right['22'] = {
      showAll: true,
      dependencies: dependencies(
        Array.from({ length: 4 }, (_, i) =>
          link(`x${i}`, identity, nav(`p${i}`, `Provider ${i}`, 100 + i)),
        ),
      ),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
      { maxPerSide: 3 },
    )

    // Assert
    expect(graph.edges).toHaveLength(5)
    expect(node(graph, overflowNodeId('right', '22'))).toBeUndefined()
  })

  it("draws the rest of the subject's own column once asked", () => {
    // Arrange
    const links = Array.from({ length: 5 }, (_, i) =>
      link(`a${i}`, self, nav(`p${i}`, `Provider ${i}`, 100 + i)),
    )

    // Act
    const graph = buildWith(dependencies(links), noExpansions(), {
      maxPerSide: 3,
      subjectShowAll: ['right'],
    })

    // Assert
    expect(graph.edges).toHaveLength(5)
    expect(graph.hiddenCount).toBe(0)
  })

  it('draws nothing an expansion reached once the product that reached it is collapsed', () => {
    // Arrange — Directory is still in the expansion state, but nothing on the map leads to it.
    const directory = nav('40', 'Directory', 20)
    const expansions = noExpansions()
    expansions.right['40'] = {
      dependencies: dependencies([link('y', directory, nav('41', 'LDAP', 21))]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
    )

    // Assert
    expect(graph.placed.right).toEqual(['22'])
    expect(graph.edges.map((e) => e.id)).toEqual(['a'])
  })

  it('applies the strength filter to an expansion too', () => {
    // Arrange
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([
        link('x', identity, nav('40', 'Directory', 20), {
          strength: DependencyStrength.Soft,
        }),
      ]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, identity)]),
      expansions,
      { strengthFilter: 'hard' },
    )

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['a'])
    expect(
      (node(graph, 'right:22')!.data as DependencyNodeData).expansion,
    ).toBe('empty')
  })

  it('keeps an expansion beside the product it grew from', () => {
    // Arrange — Alpha sorts first but was expanded from the lower of the two providers.
    const upper = nav('91', 'Aardvark', 91)
    const lower = nav('92', 'Zebra', 92)
    const expansions = noExpansions()
    expansions.right['91'] = {
      dependencies: dependencies([link('u', upper, nav('93', 'Zulu', 93))]),
    }
    expansions.right['92'] = {
      dependencies: dependencies([link('l', lower, nav('94', 'Alpha', 94))]),
    }

    // Act
    const graph = buildWith(
      dependencies([link('a', self, upper), link('b', self, lower)]),
      expansions,
    )

    // Assert
    expect(node(graph, 'right:93')!.position.y).toBeLessThan(
      node(graph, 'right:94')!.position.y,
    )
  })

  it('draws a platform once per column it holds products in', () => {
    // Arrange — one of the platform's services is depended on directly, another two hops out.
    const platform = nav('20', 'Core Platform', 30)
    const search = nav('23', 'Search Service', 13)
    const expansions = noExpansions()
    expansions.right['22'] = {
      dependencies: dependencies([
        link('x', identity, search, { dependsOnProductPath: [platform] }),
      ]),
    }

    // Act
    const graph = buildWith(
      dependencies([
        link('a', self, identity, { dependsOnProductPath: [platform] }),
      ]),
      expansions,
    )

    // Assert
    expect(inside(graph.nodes, '20', 'right')).toEqual(['right:22'])
    expect(
      graph.nodes
        .filter((n) => n.parentId === groupNodeId('right', '20', 2))
        .map((n) => n.id),
    ).toEqual(['right:23'])
  })

  it('explains the box only when the subject is drawn as one', () => {
    // Arrange — the only box is the provider's own platform.
    const platform = nav('20', 'Core Platform', 30)

    // Act
    const graph = buildWith(
      dependencies([
        link('a', self, identity, { dependsOnProductPath: [platform] }),
      ]),
      noExpansions(),
    )

    // Assert
    expect(graph.hasContainedRecords).toBe(false)
  })
})
