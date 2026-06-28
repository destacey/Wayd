import { groupDepths, maxGroupDepth, resolveLevel } from './depth'
import type { TimelineGroup, TimelineItem } from './types'

const g = (
  id: string,
  treeLevel: number,
  parentId?: string,
): TimelineGroup => ({ id, parentId, treeLevel })

// Tree (1-based treeLevel): a(1) -> a1(2) -> a1a(3) ; b(1, no children)
const groups: TimelineGroup[] = [
  g('a', 1),
  g('a1', 2, 'a'),
  g('a1a', 3, 'a1'),
  g('b', 1),
]

// Each activity is a range item assigned to its own group, carrying treeLevel.
const item = (id: string, groupId: string, treeLevel: number): TimelineItem => ({
  id,
  kind: 'range',
  start: 0,
  end: 1,
  groupId,
  treeLevel,
})
const items: TimelineItem[] = [
  item('ia', 'a', 1),
  item('ia1', 'a1', 2),
  item('ia1a', 'a1a', 3),
  item('ib', 'b', 1),
  // A milestone (decoration) under a1 → treeLevel = a1's level + 1 = 3.
  { id: 'm', kind: 'milestone', start: 0, end: 0, groupId: 'a1', treeLevel: 3 },
]

describe('groupDepths', () => {
  it('computes 0-based depth from the parent chain', () => {
    // Arrange / Act
    const d = groupDepths(groups)
    // Assert
    expect(d.get('a')).toBe(0)
    expect(d.get('a1')).toBe(1)
    expect(d.get('a1a')).toBe(2)
    expect(d.get('b')).toBe(0)
  })

  it('treats a missing parent as a root', () => {
    // Arrange — parent id not in the set.
    const orphan = [g('x', 1, 'ghost')]
    // Act / Assert
    expect(groupDepths(orphan).get('x')).toBe(0)
  })
})

describe('maxGroupDepth', () => {
  it('returns the deepest level', () => {
    // Arrange / Act / Assert
    expect(maxGroupDepth(groups)).toBe(2)
  })

  it('is 0 for a flat set', () => {
    // Arrange / Act / Assert
    expect(maxGroupDepth([g('a', 1), g('b', 1)])).toBe(0)
  })
})

describe('resolveLevel', () => {
  it('level 1 → no groups; only treeLevel-1 activity bars, flat', () => {
    // Arrange / Act
    const out = resolveLevel(items, groups, 1)
    // Assert — no groups; bars are the level-1 activities (ia, ib), flat.
    expect(out.groups).toHaveLength(0)
    const ids = out.items.map((i) => i.id).sort()
    expect(ids).toEqual(['ia', 'ib'])
    expect(out.items.every((i) => i.groupId === undefined)).toBe(true)
  })

  it('level 2 → level-1 activities are groups; level-2 items are bars', () => {
    // Arrange / Act
    const out = resolveLevel(items, groups, 2)
    // Assert — groups: a, b (treeLevel 1). NOT a1 (that's a bar now).
    expect(out.groups.map((x) => x.id).sort()).toEqual(['a', 'b'])
    const ids = out.items.map((i) => i.id).sort()
    // Bars: ia1 (level-2 activity). NOT ia/ib (they're groups now), NOT ia1a
    // (level 3, hidden), NOT m (decoration level 3 > 2, hidden).
    expect(ids).toEqual(['ia1'])
    // ia1 sits under its surviving parent group 'a'.
    expect(out.items[0].groupId).toBe('a')
  })

  it('level 3 → level-1,2 activities are groups; level-3 items + decorations are bars', () => {
    // Arrange / Act
    const out = resolveLevel(items, groups, 3)
    // Assert — groups: a, a1, b (treeLevel < 3).
    expect(out.groups.map((x) => x.id).sort()).toEqual(['a', 'a1', 'b'])
    const ids = out.items.map((i) => i.id).sort()
    // Bars: ia1a (level-3 activity) + m (level-3 milestone). a1a not a group.
    expect(ids).toEqual(['ia1a', 'm'])
    const byId = new Map(out.items.map((i) => [i.id, i]))
    expect(byId.get('ia1a')?.groupId).toBe('a1') // under surviving parent
    expect(byId.get('m')?.groupId).toBe('a1')
  })

  it('a level-1 activity (no children) is a BAR at level 1, hidden at level 2', () => {
    // Arrange — 'b' has no children. Act/Assert.
    const l1 = resolveLevel(items, groups, 1)
    expect(l1.items.some((i) => i.id === 'ib')).toBe(true) // bar at level 1
    const l2 = resolveLevel(items, groups, 2)
    // At level 2, b becomes a group; its own bar 'ib' is NOT shown (no level-2
    // child to render) — b is just an (empty) group row.
    expect(l2.groups.some((x) => x.id === 'b')).toBe(true)
    expect(l2.items.some((i) => i.id === 'ib')).toBe(false)
  })

  it('decorations show whenever treeLevel <= level (never become groups)', () => {
    // Arrange — milestone m at treeLevel 3.
    // Act / Assert — hidden at level 2, shown at level 3.
    expect(resolveLevel(items, groups, 2).items.some((i) => i.id === 'm')).toBe(false)
    expect(resolveLevel(items, groups, 3).items.some((i) => i.id === 'm')).toBe(true)
  })

  it('clamps level below 1 up to 1', () => {
    // Arrange / Act / Assert
    expect(resolveLevel(items, groups, 0).groups).toHaveLength(0)
  })

  it('excludes a treeLevel-0 root band at level 1 (its decoration goes ungrouped)', () => {
    // Arrange — synthetic root band group + a root-level background under it.
    const root: TimelineGroup = { id: 'root', treeLevel: 0, label: '' }
    const rootBg: TimelineItem = {
      id: 'tb',
      kind: 'background',
      start: 0,
      end: 1,
      groupId: 'root',
      treeLevel: 1,
    }
    const g2 = [root, ...groups]
    const i2 = [...items, rootBg]
    // Act — level 1: nothing is a group (flat); root bg falls through ungrouped.
    const l1 = resolveLevel(i2, g2, 1)
    // Assert
    expect(l1.groups).toHaveLength(0)
    expect(l1.items.find((i) => i.id === 'tb')?.groupId).toBeUndefined()
    // Level 2: root band survives as a group; bg scopes to it.
    const l2 = resolveLevel(i2, g2, 2)
    expect(l2.groups.some((x) => x.id === 'root')).toBe(true)
    expect(l2.items.find((i) => i.id === 'tb')?.groupId).toBe('root')
  })
})
