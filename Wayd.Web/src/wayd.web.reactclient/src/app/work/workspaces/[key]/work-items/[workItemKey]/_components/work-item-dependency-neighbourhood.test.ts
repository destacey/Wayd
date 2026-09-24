import {
  groupNodeId,
  overflowNodeId,
  type DependencyEdgeData,
  type DependencyNodeData,
} from '@/src/components/common/dependency-map/dependency-neighbourhood'
import {
  ScopedDependencyDto,
  WorkItemDetailsNavigationDto,
  WorkTeamNavigationDto,
} from '@/src/services/wayd-api'
import {
  buildWorkItemDependencyNeighbourhood,
  variantOf,
  type WorkItemDependencyExpansions,
  type WorkItemMapRef,
} from './work-item-dependency-neighbourhood'

const subject: WorkItemMapRef = {
  id: 'w-1',
  key: 'APP-1',
  title: 'Checkout',
  workspaceKey: 'APP',
}

const team = (id: string, name: string): WorkTeamNavigationDto => ({
  id,
  key: Number(id.replace(/\D/g, '')) || 1,
  name,
  code: name.slice(0, 3).toUpperCase(),
  type: 'Team',
})

const payments = team('t-1', 'Payments')
const identity = team('t-2', 'Identity')

const item = (
  id: string,
  key: string,
  title: string,
  withTeam?: WorkTeamNavigationDto,
): WorkItemDetailsNavigationDto => ({
  id,
  key,
  title,
  workspaceKey: key.split('-')[0],
  type: 'Story',
  status: 'Active',
  statusCategory: { id: 2, name: 'Active' },
  team: withTeam,
})

const dependency = (
  id: string,
  other: WorkItemDetailsNavigationDto,
  type: 'Predecessor' | 'Successor',
  overrides: Partial<ScopedDependencyDto> = {},
): ScopedDependencyDto => ({
  id,
  dependency: other,
  type,
  state: { id: 1, name: 'To Do' },
  health: { id: 1, name: 'Healthy' },
  scope: { id: 2, name: 'Cross-Team' },
  createdOn: new Date('2026-01-01'),
  ...overrides,
})

const noExpansions = (): WorkItemDependencyExpansions => ({
  left: {},
  right: {},
})

const auth = item('w-2', 'ID-7', 'Token service', identity)
const refund = item('w-3', 'APP-9', 'Refund flow', payments)

describe('buildWorkItemDependencyNeighbourhood', () => {
  it('puts predecessors left of the work item and successors right of it', () => {
    // Arrange
    const dependencies = [
      dependency('d1', auth, 'Predecessor'),
      dependency('d2', refund, 'Successor'),
    ]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
    })

    // Assert
    const byId = Object.fromEntries(graph.nodes.map((n) => [n.id, n]))
    expect((byId['left:w-2'].data as DependencyNodeData).side).toBe('left')
    expect((byId['right:w-3'].data as DependencyNodeData).side).toBe('right')
  })

  it('points every arrow from the predecessor to the successor', () => {
    // Arrange
    const dependencies = [
      dependency('d1', auth, 'Predecessor'),
      dependency('d2', refund, 'Successor'),
    ]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
    })

    // Assert
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual(
      expect.arrayContaining([
        ['left:w-2', 'w-1'],
        ['w-1', 'right:w-3'],
      ]),
    )
  })

  it('draws each far work item inside its team, linked to the team', () => {
    // Arrange
    const dependencies = [dependency('d1', auth, 'Predecessor')]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
    })

    // Assert
    const box = graph.nodes.find((n) => n.id === groupNodeId('left', 't-2'))
    expect(box?.data).toEqual(
      expect.objectContaining({
        label: 'Identity',
        href: '/organizations/teams/2',
      }),
    )
    expect(graph.nodes.find((n) => n.id === 'left:w-2')?.parentId).toBe(box?.id)
  })

  it('draws the work item inside its own team, as it draws the far ones', () => {
    // Arrange
    const dependencies = [dependency('d1', refund, 'Successor')]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: { ...subject, team: payments },
      dependencies,
    })

    // Assert
    const box = graph.nodes.find((n) => n.id === groupNodeId('center', 't-1'))
    expect(box?.data).toEqual(
      expect.objectContaining({
        label: 'Payments',
        href: '/organizations/teams/1',
      }),
    )
    expect(graph.nodes.find((n) => n.id === 'w-1')?.parentId).toBe(box?.id)
    // Each column draws its own box, even for the same team.
    expect(graph.nodes.find((n) => n.id === 'right:w-3')?.parentId).toBe(
      groupNodeId('right', 't-1'),
    )
  })

  it('labels a work item by key and title and links it to its page', () => {
    // Arrange
    const dependencies = [dependency('d1', auth, 'Predecessor')]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
    })

    // Assert
    expect(graph.nodes.find((n) => n.id === 'left:w-2')?.data).toEqual(
      expect.objectContaining({
        label: 'ID-7 · Token service',
        href: '/work/workspaces/ID/work-items/ID-7',
      }),
    )
  })

  it('draws a work item with no team without a box', () => {
    // Arrange
    const loose = item('w-4', 'APP-4', 'Unassigned')

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies: [dependency('d1', loose, 'Successor')],
    })

    // Assert
    expect(graph.nodes.find((n) => n.id === 'right:w-4')?.parentId).toBe(
      undefined,
    )
    expect(graph.nodes.some((n) => n.type === 'recordGroup')).toBe(false)
  })

  it('leaves out done links when only open ones are asked for', () => {
    // Arrange
    const dependencies = [
      dependency('d1', auth, 'Predecessor', {
        state: { id: 3, name: 'Done' },
      }),
      dependency('d2', refund, 'Successor'),
    ]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
      filter: 'open',
    })

    // Assert
    expect(graph.edges.map((e) => e.id)).toEqual(['d2'])
  })

  it('says a count is of open links while the filter is on', () => {
    // Arrange
    const dependencies = Array.from({ length: 3 }, (_, i) =>
      dependency(
        `d${i}`,
        item(`w-${10 + i}`, `APP-${10 + i}`, `Item ${i}`),
        'Successor',
      ),
    )

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
      filter: 'open',
      maxPerSide: 2,
    })

    // Assert
    expect(
      graph.nodes.find((n) => n.id === overflowNodeId('right', null))?.data,
    ).toEqual(expect.objectContaining({ label: '+1 more open' }))
  })

  it('follows a predecessor back to what it waits on, not to what waits on it', () => {
    // Arrange
    const upstream = item('w-5', 'ID-2', 'Key rotation', identity)
    const sibling = item('w-6', 'APP-6', 'Receipts', payments)
    const expansions = noExpansions()
    expansions.left['w-2'] = {
      workItem: auth,
      dependencies: [
        dependency('e1', upstream, 'Predecessor'),
        dependency('e2', sibling, 'Successor'),
      ],
    }

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies: [dependency('d1', auth, 'Predecessor')],
      expansions,
    })

    // Assert
    expect(graph.edges.map((e) => [e.source, e.target])).toContainEqual([
      'left:w-5',
      'left:w-2',
    ])
    expect(graph.nodes.some((n) => n.id.endsWith('w-6'))).toBe(false)
    // Both sit inside team boxes, whose positions their own are relative to.
    const absoluteX = (id: string): number => {
      const node = graph.nodes.find((n) => n.id === id)!
      return node.position.x + (node.parentId ? absoluteX(node.parentId) : 0)
    }
    expect(absoluteX('left:w-5')).toBeLessThan(absoluteX('left:w-2'))
  })

  it('carries each link’s variant onto its edge', () => {
    // Arrange
    const dependencies = [
      dependency('d1', auth, 'Predecessor', {
        health: { id: 3, name: 'Unhealthy' },
      }),
    ]

    // Act
    const graph = buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
    })

    // Assert
    expect((graph.edges[0].data as DependencyEdgeData).variant).toBe(
      'Unhealthy',
    )
  })
})

describe('variantOf', () => {
  it('draws a link as its health while the predecessor is not done', () => {
    // Arrange
    const link = dependency('d1', auth, 'Predecessor', {
      health: { id: 2, name: 'At Risk' },
    })

    // Act
    const variant = variantOf(link)

    // Assert
    expect(variant).toBe('At Risk')
  })

  it('draws a link as done once the predecessor is, whatever its health', () => {
    // Arrange
    const link = dependency('d1', auth, 'Predecessor', {
      state: { id: 3, name: 'Done' },
      health: { id: 1, name: 'Healthy' },
    })

    // Act
    const variant = variantOf(link)

    // Assert
    expect(variant).toBe('Done')
  })

  it('falls back to unknown for a health the map has no style for', () => {
    // Arrange
    const link = dependency('d1', auth, 'Predecessor', {
      health: { id: 9, name: 'Something New' },
    })

    // Act
    const variant = variantOf(link)

    // Assert
    expect(variant).toBe('Unknown')
  })
})
