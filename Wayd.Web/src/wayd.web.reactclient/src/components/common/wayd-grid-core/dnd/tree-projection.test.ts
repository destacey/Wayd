import {
  defaultMoveValidator,
  getProjection,
  calculateOrderInParent,
  updateNodePlacement,
  INDENTATION_WIDTH,
  type TreeNode,
  type FlattenedTreeNode,
  type MoveValidator,
} from './tree-projection'

interface TestNode extends TreeNode {
  name: string
  type?: { id: number; name: string }
}

const createNode = (
  id: string,
  name: string,
  typeName: string = 'Task',
  children: TestNode[] = [],
): TestNode => ({
  id,
  name,
  type: { id: 1, name: typeName },
  children,
})

const toFlat = (
  node: TestNode,
  depth: number,
  parentId: string | null,
  ancestorIds: string[],
  flatIndex: number,
): FlattenedTreeNode<TestNode> => ({
  node,
  depth,
  parentId,
  ancestorIds,
  flatIndex,
})

describe('tree-projection', () => {
  describe('defaultMoveValidator', () => {
    it('allows move to root level', () => {
      const active = toFlat(createNode('1', 'A'), 1, 'parent', ['parent'], 0)
      const result = defaultMoveValidator(active, null, null)
      expect(result.canMove).toBe(true)
    })

    it('prevents node from being its own parent', () => {
      const node = createNode('1', 'A')
      const active = toFlat(node, 0, null, [], 0)
      const result = defaultMoveValidator(active, node as any, '1')
      expect(result.canMove).toBe(false)
      expect(result.reason).toContain('own parent')
    })

    it('prevents circular reference (moving parent into descendant)', () => {
      const active = toFlat(createNode('1', 'A'), 0, null, [], 0)
      // Target parent has '1' in its ancestors (it's a descendant of active)
      const targetParent = {
        ...toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
      }
      const result = defaultMoveValidator(
        active,
        targetParent as any,
        '1-1',
      )
      expect(result.canMove).toBe(false)
      expect(result.reason).toContain('descendants')
    })

    it('allows valid moves', () => {
      const active = toFlat(createNode('1', 'A'), 0, null, [], 0)
      const target = toFlat(createNode('2', 'B'), 0, null, [], 1)
      const result = defaultMoveValidator(active, target as any, '2')
      expect(result.canMove).toBe(true)
    })
  })

  describe('getProjection', () => {
    it('calculates projection for same-level move', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
        toFlat(createNode('3', 'C'), 0, null, [], 2),
      ]

      const projection = getProjection(flatItems, '1', '3', 0, INDENTATION_WIDTH)

      expect(projection.depth).toBe(0)
      expect(projection.parentId).toBeNull()
      expect(projection.canDrop).toBe(true)
    })

    it('calculates projection for depth increase (make child)', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
      ]

      // Drag task 2 over itself with positive offset (looks back to find task 1)
      const projection = getProjection(
        flatItems,
        '2',
        '2',
        INDENTATION_WIDTH,
        INDENTATION_WIDTH,
      )

      expect(projection.depth).toBe(1)
      expect(projection.parentId).toBe('1')
      expect(projection.canDrop).toBe(true)
    })

    it('makes node 3 child of node 1 when hovering over node 2 with right drag', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
        toFlat(createNode('3', 'C'), 0, null, [], 2),
      ]

      const projection = getProjection(
        flatItems,
        '3',
        '2',
        INDENTATION_WIDTH,
        INDENTATION_WIDTH,
      )

      expect(projection.depth).toBe(1)
      expect(projection.parentId).toBe('1')
      expect(projection.canDrop).toBe(true)
    })

    it('makes node 3 child of node 2 when hovering over node 3 with right drag', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
        toFlat(createNode('3', 'C'), 0, null, [], 2),
      ]

      const projection = getProjection(
        flatItems,
        '3',
        '3',
        INDENTATION_WIDTH,
        INDENTATION_WIDTH,
      )

      expect(projection.depth).toBe(1)
      expect(projection.parentId).toBe('2')
      expect(projection.canDrop).toBe(true)
    })

    it('prevents projection that creates circular reference', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
        toFlat(createNode('1-1-1', 'A.1.1'), 2, '1-1', ['1', '1-1'], 2),
      ]

      const projection = getProjection(
        flatItems,
        '1',
        '1-1-1',
        INDENTATION_WIDTH * 2,
        INDENTATION_WIDTH,
      )

      expect(projection.canDrop).toBe(false)
      expect(projection.reason).toContain('descendants')
    })

    it('uses custom validator to reject moves', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'Milestone', 'Milestone'), 0, null, [], 0),
        toFlat(createNode('2', 'Task'), 0, null, [], 1),
        toFlat(createNode('3', 'Task'), 0, null, [], 2),
      ]

      const milestoneValidator: MoveValidator<TestNode> = (
        _active,
        targetParent,
        targetParentId,
      ) => {
        if (!targetParentId) return { canMove: true }
        // Get the actual node from the flattened wrapper
        const parentNode =
          (targetParent as any)?.node ?? targetParent
        if (parentNode?.type?.name === 'Milestone') {
          return {
            canMove: false,
            reason: 'Milestones cannot have children',
          }
        }
        return defaultMoveValidator(_active, targetParent, targetParentId)
      }

      const projection = getProjection(
        flatItems,
        '3',
        '2',
        INDENTATION_WIDTH,
        INDENTATION_WIDTH,
        milestoneValidator,
      )

      expect(projection.canDrop).toBe(false)
      expect(projection.reason).toContain('Milestones')
    })

    it('handles invalid node IDs gracefully', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
      ]

      const projection = getProjection(
        flatItems,
        'invalid',
        '1',
        0,
        INDENTATION_WIDTH,
      )

      expect(projection.canDrop).toBe(false)
      expect(projection.reason).toContain('not found')
    })

    it('promotes only child to sibling of parent (drag left)', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
      ]

      const projection = getProjection(
        flatItems,
        '1-1',
        '1-1',
        -INDENTATION_WIDTH,
        INDENTATION_WIDTH,
      )

      expect(projection.canDrop).toBe(true)
      expect(projection.depth).toBe(0)
      expect(projection.parentId).toBe(null)
    })

    it('promotes deeply nested child one level up', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
        toFlat(createNode('1-1-1', 'A.1.1'), 2, '1-1', ['1', '1-1'], 2),
      ]

      const projection = getProjection(
        flatItems,
        '1-1-1',
        '1-1-1',
        -INDENTATION_WIDTH,
        INDENTATION_WIDTH,
      )

      expect(projection.canDrop).toBe(true)
      expect(projection.depth).toBe(1)
      expect(projection.parentId).toBe('1')
    })
  })

  describe('calculateOrderInParent', () => {
    it('calculates order for root level nodes (1-based)', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
        toFlat(createNode('3', 'C'), 0, null, [], 2),
      ]

      const order = calculateOrderInParent(flatItems, '1', 1, null)
      expect(order).toBe(2) // After node 2
    })

    it('calculates order for child nodes (1-based)', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
        toFlat(createNode('1-2', 'A.2'), 1, '1', ['1'], 2),
        toFlat(createNode('1-3', 'A.3'), 1, '1', ['1'], 3),
      ]

      const order = calculateOrderInParent(flatItems, '1-1', 2, '1')
      expect(order).toBe(2) // After node 1-2
    })

    it('returns 1 when no siblings exist', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
      ]

      const order = calculateOrderInParent(flatItems, 'new-node', 0, null)
      expect(order).toBe(2) // After the only item
    })
  })

  describe('updateNodePlacement', () => {
    it('updates node parent and depth', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('2', 'B'), 0, null, [], 1),
      ]

      const result = updateNodePlacement(flatItems, '2', '1', 1)

      expect(result[1]).toMatchObject({
        depth: 1,
        parentId: '1',
        ancestorIds: ['1'],
      })
      // First item unchanged
      expect(result[0]).toMatchObject({
        depth: 0,
        parentId: null,
        ancestorIds: [],
      })
    })

    it('updates ancestorIds correctly for nested parents', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
        toFlat(createNode('2', 'B'), 0, null, [], 2),
      ]

      const result = updateNodePlacement(flatItems, '2', '1-1', 2)

      expect(result[2]).toMatchObject({
        depth: 2,
        parentId: '1-1',
        ancestorIds: ['1', '1-1'],
      })
    })

    it('handles moving to root level', () => {
      const flatItems: FlattenedTreeNode<TestNode>[] = [
        toFlat(createNode('1', 'A'), 0, null, [], 0),
        toFlat(createNode('1-1', 'A.1'), 1, '1', ['1'], 1),
      ]

      const result = updateNodePlacement(flatItems, '1-1', null, 0)

      expect(result[1]).toMatchObject({
        depth: 0,
        parentId: null,
        ancestorIds: [],
      })
    })
  })
})
