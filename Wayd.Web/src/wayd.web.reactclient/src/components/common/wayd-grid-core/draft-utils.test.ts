import type { TreeNode } from './dnd/tree-projection'
import { mergeDraftsIntoTree, type DraftItem } from './draft-utils'

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

const createDraftNode = (draft: DraftItem): TestNode => ({
  id: draft.id,
  name: '',
  children: [],
  parentId: draft.parentId,
})

describe('draft-utils', () => {
  describe('mergeDraftsIntoTree', () => {
    it('returns original tree when no drafts', () => {
      const tree = [createNode('1', 'A'), createNode('2', 'B')]
      const result = mergeDraftsIntoTree(tree, [], createDraftNode)
      expect(result).toBe(tree) // Same reference, no copy
    })

    it('appends root-level draft', () => {
      const tree = [createNode('1', 'A'), createNode('2', 'B')]
      const drafts: DraftItem[] = [
        { id: 'draft-1', parentId: undefined, order: 0 },
      ]

      const result = mergeDraftsIntoTree(tree, drafts, createDraftNode)

      expect(result).toHaveLength(3)
      expect(result[0].id).toBe('1')
      expect(result[1].id).toBe('2')
      expect(result[2].id).toBe('draft-1')
    })

    it('inserts child draft under correct parent', () => {
      const tree = [
        createNode('1', 'A', [createNode('1-1', 'A.1')]),
        createNode('2', 'B'),
      ]
      const drafts: DraftItem[] = [
        { id: 'draft-1', parentId: '1', order: 0 },
      ]

      const result = mergeDraftsIntoTree(tree, drafts, createDraftNode)

      expect(result).toHaveLength(2) // Root count unchanged
      expect(result[0].children).toHaveLength(2) // Draft added as child
      expect(result[0].children[0].id).toBe('1-1')
      expect(result[0].children[1].id).toBe('draft-1')
    })

    it('handles multiple drafts at different levels', () => {
      const tree = [createNode('1', 'A', [createNode('1-1', 'A.1')])]
      const drafts: DraftItem[] = [
        { id: 'draft-root', parentId: undefined, order: 0 },
        { id: 'draft-child', parentId: '1-1', order: 0 },
      ]

      const result = mergeDraftsIntoTree(tree, drafts, createDraftNode)

      expect(result).toHaveLength(2) // 1 original + 1 root draft
      expect(result[1].id).toBe('draft-root')
      expect((result[0].children[0] as TestNode).children).toHaveLength(1)
      expect((result[0].children[0] as TestNode).children[0].id).toBe(
        'draft-child',
      )
    })

    it('calls createDraftNode factory for each draft', () => {
      const tree = [createNode('1', 'A')]
      const drafts: DraftItem[] = [
        { id: 'draft-1', parentId: undefined, order: 0 },
        { id: 'draft-2', parentId: undefined, order: 1 },
      ]
      const factory = jest.fn(createDraftNode)

      mergeDraftsIntoTree(tree, drafts, factory)

      expect(factory).toHaveBeenCalledTimes(2)
      expect(factory).toHaveBeenCalledWith(drafts[0])
      expect(factory).toHaveBeenCalledWith(drafts[1])
    })

    it('does not mutate original tree nodes when adding child drafts', () => {
      const child = createNode('1-1', 'A.1')
      const parent = createNode('1', 'A', [child])
      const tree = [parent]
      const drafts: DraftItem[] = [
        { id: 'draft-1', parentId: '1', order: 0 },
      ]

      const result = mergeDraftsIntoTree(tree, drafts, createDraftNode)

      // Original parent should not have been mutated
      expect(parent.children).toHaveLength(1)
      // Result should have a new parent object with 2 children
      expect(result[0].children).toHaveLength(2)
      expect(result[0]).not.toBe(parent)
    })
  })
})
