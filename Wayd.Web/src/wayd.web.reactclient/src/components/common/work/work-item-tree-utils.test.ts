import { WorkItemListDto } from '@/src/services/api/wayd.schemas'
import { buildWorkItemTree } from './work-item-tree-utils'

/** Creates a minimal WorkItemListDto for testing. */
function createWorkItem(
  id: string,
  key: string,
  parentId?: string,
  parentKey?: string,
): WorkItemListDto {
  return {
    id,
    key,
    title: `Title for ${key}`,
    workspace: { id: 'ws-1', key: 'WS', name: 'Workspace' },
    type: { id: 1, name: 'User Story' },
    status: 'Active',
    statusCategory: { id: 1, name: 'Active' },
    stackRank: 0,
    created: new Date(),
    ...(parentId && {
      parent: {
        id: parentId,
        key: parentKey ?? 'PARENT',
        title: 'Parent',
        workspaceKey: 'WS',
      },
    }),
  } as WorkItemListDto
}

describe('buildWorkItemTree', () => {
  it('returns empty array for empty input', () => {
    const result = buildWorkItemTree([])
    expect(result).toEqual([])
  })

  it('treats all items as roots when none have parents', () => {
    const items = [
      createWorkItem('1', 'WS-1'),
      createWorkItem('2', 'WS-2'),
      createWorkItem('3', 'WS-3'),
    ]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(3)
    expect(result.every((n) => n.children.length === 0)).toBe(true)
    expect(result.every((n) => n.parentId === null)).toBe(true)
  })

  it('nests children under their parent', () => {
    const items = [
      createWorkItem('epic-1', 'WS-1'),
      createWorkItem('story-1', 'WS-2', 'epic-1', 'WS-1'),
      createWorkItem('story-2', 'WS-3', 'epic-1', 'WS-1'),
    ]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(1)
    expect(result[0].key).toBe('WS-1')
    expect(result[0].children).toHaveLength(2)
    expect(result[0].children.map((c) => c.key)).toEqual(['WS-2', 'WS-3'])
  })

  it('supports multiple levels of nesting', () => {
    const items = [
      createWorkItem('epic-1', 'WS-1'),
      createWorkItem('feature-1', 'WS-2', 'epic-1', 'WS-1'),
      createWorkItem('story-1', 'WS-3', 'feature-1', 'WS-2'),
    ]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(1)
    expect(result[0].children).toHaveLength(1)
    expect(result[0].children[0].children).toHaveLength(1)
    expect(result[0].children[0].children[0].key).toBe('WS-3')
  })

  it('treats items with missing parents as roots', () => {
    const items = [
      createWorkItem('story-1', 'WS-1', 'missing-parent', 'WS-99'),
      createWorkItem('story-2', 'WS-2', 'missing-parent', 'WS-99'),
    ]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(2)
    expect(result.every((n) => n.parentId === null)).toBe(true)
  })

  it('handles mix of roots, matched parents, and missing parents', () => {
    const items = [
      createWorkItem('epic-1', 'WS-1'), // root (no parent)
      createWorkItem('story-1', 'WS-2', 'epic-1', 'WS-1'), // child of epic-1
      createWorkItem('story-2', 'WS-3', 'missing', 'WS-99'), // orphan → root
    ]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(2)
    const epic = result.find((n) => n.key === 'WS-1')
    const orphan = result.find((n) => n.key === 'WS-3')
    expect(epic).toBeDefined()
    expect(epic!.children).toHaveLength(1)
    expect(epic!.children[0].key).toBe('WS-2')
    expect(orphan).toBeDefined()
    expect(orphan!.children).toHaveLength(0)
  })

  it('preserves sibling order based on input order', () => {
    const items = [
      createWorkItem('epic-1', 'WS-1'),
      createWorkItem('story-a', 'WS-A', 'epic-1', 'WS-1'),
      createWorkItem('story-b', 'WS-B', 'epic-1', 'WS-1'),
      createWorkItem('story-c', 'WS-C', 'epic-1', 'WS-1'),
    ]

    const result = buildWorkItemTree(items)

    expect(result[0].children.map((c) => c.key)).toEqual([
      'WS-A',
      'WS-B',
      'WS-C',
    ])
  })

  it('handles items with null parent gracefully', () => {
    const items = [createWorkItem('1', 'WS-1')]

    const result = buildWorkItemTree(items)

    expect(result).toHaveLength(1)
    expect(result[0].parentId).toBeNull()
    expect(result[0].children).toHaveLength(0)
  })
})
