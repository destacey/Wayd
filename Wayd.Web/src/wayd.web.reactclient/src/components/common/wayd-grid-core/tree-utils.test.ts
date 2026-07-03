import type { TreeNode, FlattenedTreeNode } from './dnd/tree-projection'
import { countTreeNodes, findNodeById, flattenTree, buildTree } from './tree-utils'

interface TestNode extends TreeNode {
  name: string
}

const createNode = (
  id: string,
  name: string,
  children: TestNode[] = [],
): TestNode => ({
  id,
  name,
  children,
})

describe('tree-utils', () => {
  describe('countTreeNodes', () => {
    it('returns 0 for empty tree', () => {
      expect(countTreeNodes([])).toBe(0)
    })

    it('counts flat list', () => {
      const nodes = [createNode('1', 'A'), createNode('2', 'B'), createNode('3', 'C')]
      expect(countTreeNodes(nodes)).toBe(3)
    })

    it('counts nested tree', () => {
      const tree = [
        createNode('1', 'A', [
          createNode('1-1', 'A.1', [createNode('1-1-1', 'A.1.1')]),
          createNode('1-2', 'A.2'),
        ]),
        createNode('2', 'B'),
      ]
      expect(countTreeNodes(tree)).toBe(5)
    })

    it('counts deeply nested single chain', () => {
      const tree = [
        createNode('1', 'A', [
          createNode('2', 'B', [createNode('3', 'C', [createNode('4', 'D')])]),
        ]),
      ]
      expect(countTreeNodes(tree)).toBe(4)
    })
  })

  describe('findNodeById', () => {
    const tree = [
      createNode('1', 'A', [
        createNode('1-1', 'A.1', [createNode('1-1-1', 'A.1.1')]),
        createNode('1-2', 'A.2'),
      ]),
      createNode('2', 'B'),
    ]

    it('finds root node', () => {
      const found = findNodeById(tree, '1')
      expect(found).not.toBeNull()
      expect(found!.name).toBe('A')
    })

    it('finds nested node', () => {
      const found = findNodeById(tree, '1-1-1')
      expect(found).not.toBeNull()
      expect(found!.name).toBe('A.1.1')
    })

    it('returns null for nonexistent id', () => {
      expect(findNodeById(tree, 'nonexistent')).toBeNull()
    })

    it('returns null for empty tree', () => {
      expect(findNodeById([], '1')).toBeNull()
    })
  })

  describe('flattenTree', () => {
    it('returns empty array for empty tree', () => {
      expect(flattenTree([])).toHaveLength(0)
    })

    it('flattens a simple tree with no children', () => {
      const tree = [createNode('1', 'A'), createNode('2', 'B')]

      const result = flattenTree(tree)

      expect(result).toHaveLength(2)
      expect(result[0]).toMatchObject({
        node: expect.objectContaining({ id: '1' }),
        depth: 0,
        parentId: null,
        ancestorIds: [],
        flatIndex: 0,
      })
      expect(result[1]).toMatchObject({
        node: expect.objectContaining({ id: '2' }),
        depth: 0,
        parentId: null,
        ancestorIds: [],
        flatIndex: 1,
      })
    })

    it('flattens a tree with nested children', () => {
      const tree = [
        createNode('1', 'A', [
          createNode('1-1', 'A.1', [createNode('1-1-1', 'A.1.1')]),
          createNode('1-2', 'A.2'),
        ]),
        createNode('2', 'B'),
      ]

      const result = flattenTree(tree)

      expect(result).toHaveLength(5)

      expect(result[0]).toMatchObject({
        node: expect.objectContaining({ id: '1' }),
        depth: 0,
        parentId: null,
        ancestorIds: [],
      })
      expect(result[1]).toMatchObject({
        node: expect.objectContaining({ id: '1-1' }),
        depth: 1,
        parentId: '1',
        ancestorIds: ['1'],
      })
      expect(result[2]).toMatchObject({
        node: expect.objectContaining({ id: '1-1-1' }),
        depth: 2,
        parentId: '1-1',
        ancestorIds: ['1', '1-1'],
      })
      expect(result[3]).toMatchObject({
        node: expect.objectContaining({ id: '1-2' }),
        depth: 1,
        parentId: '1',
        ancestorIds: ['1'],
      })
      expect(result[4]).toMatchObject({
        node: expect.objectContaining({ id: '2' }),
        depth: 0,
        parentId: null,
        ancestorIds: [],
      })
    })

    it('preserves order within each level', () => {
      const tree = [
        createNode('1', 'First'),
        createNode('2', 'Second'),
        createNode('3', 'Third'),
      ]

      const result = flattenTree(tree)

      expect(result[0].node.id).toBe('1')
      expect(result[1].node.id).toBe('2')
      expect(result[2].node.id).toBe('3')
      expect(result[0].flatIndex).toBe(0)
      expect(result[1].flatIndex).toBe(1)
      expect(result[2].flatIndex).toBe(2)
    })
  })

  describe('buildTree', () => {
    it('rebuilds a simple flat array into a tree', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        {
          node: createNode('1', 'A'),
          depth: 0,
          parentId: null,
          ancestorIds: [],
          flatIndex: 0,
        },
        {
          node: createNode('2', 'B'),
          depth: 0,
          parentId: null,
          ancestorIds: [],
          flatIndex: 1,
        },
      ]

      const result = buildTree(flatItems)

      expect(result).toHaveLength(2)
      expect(result[0].id).toBe('1')
      expect(result[1].id).toBe('2')
      expect(result[0].children).toHaveLength(0)
      expect(result[1].children).toHaveLength(0)
    })

    it('rebuilds a nested tree structure', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        {
          node: createNode('1', 'A'),
          depth: 0,
          parentId: null,
          ancestorIds: [],
          flatIndex: 0,
        },
        {
          node: createNode('1-1', 'A.1'),
          depth: 1,
          parentId: '1',
          ancestorIds: ['1'],
          flatIndex: 1,
        },
        {
          node: createNode('1-2', 'A.2'),
          depth: 1,
          parentId: '1',
          ancestorIds: ['1'],
          flatIndex: 2,
        },
        {
          node: createNode('2', 'B'),
          depth: 0,
          parentId: null,
          ancestorIds: [],
          flatIndex: 3,
        },
      ]

      const result = buildTree(flatItems)

      expect(result).toHaveLength(2)
      expect(result[0].id).toBe('1')
      expect(result[0].children).toHaveLength(2)
      expect(result[0].children[0].id).toBe('1-1')
      expect(result[0].children[1].id).toBe('1-2')
      expect(result[1].id).toBe('2')
      expect(result[1].children).toHaveLength(0)
    })

    it('handles deep nesting', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        {
          node: createNode('1', 'A'),
          depth: 0,
          parentId: null,
          ancestorIds: [],
          flatIndex: 0,
        },
        {
          node: createNode('1-1', 'A.1'),
          depth: 1,
          parentId: '1',
          ancestorIds: ['1'],
          flatIndex: 1,
        },
        {
          node: createNode('1-1-1', 'A.1.1'),
          depth: 2,
          parentId: '1-1',
          ancestorIds: ['1', '1-1'],
          flatIndex: 2,
        },
      ]

      const result = buildTree(flatItems)

      expect(result).toHaveLength(1)
      expect(result[0].id).toBe('1')
      expect(result[0].children).toHaveLength(1)
      expect(result[0].children[0].id).toBe('1-1')
      expect(result[0].children[0].children).toHaveLength(1)
      expect(result[0].children[0].children[0].id).toBe('1-1-1')
    })
  })

  describe('integration: flatten and rebuild', () => {
    it('preserves tree structure through flatten and rebuild cycle', () => {
      const originalTree: TestNode[] = [
        createNode('1', 'A', [
          createNode('1-1', 'A.1', [createNode('1-1-1', 'A.1.1')]),
          createNode('1-2', 'A.2'),
        ]),
        createNode('2', 'B'),
      ]

      const flattened = flattenTree(originalTree)
      const rebuilt = buildTree(flattened)

      expect(rebuilt).toHaveLength(2)
      expect(rebuilt[0].id).toBe('1')
      expect(rebuilt[0].children).toHaveLength(2)
      expect(rebuilt[0].children[0].id).toBe('1-1')
      expect(rebuilt[0].children[0].children).toHaveLength(1)
      expect(rebuilt[0].children[0].children[0].id).toBe('1-1-1')
      expect(rebuilt[0].children[1].id).toBe('1-2')
      expect(rebuilt[1].id).toBe('2')
    })
  })
})
