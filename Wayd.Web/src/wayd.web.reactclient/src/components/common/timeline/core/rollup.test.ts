import { rollupSummaries, type RollupAccessors } from './rollup'

const DAY = 86_400_000
const day = (n: number) => n * DAY

// A minimal tree node for tests.
interface Node {
  id: string
  start?: number
  end?: number
  progress?: number
  children?: Node[]
}

const accessors: RollupAccessors<Node> = {
  id: (n) => n.id,
  children: (n) => n.children,
  start: (n) => n.start,
  end: (n) => n.end,
  progress: (n) => n.progress,
}

describe('rollupSummaries', () => {
  it('summarizes a parent from its descendant ranges', () => {
    // Arrange — parent p has no range of its own; two child ranges.
    const roots: Node[] = [
      {
        id: 'p',
        children: [
          { id: 'a', start: day(0), end: day(5) },
          { id: 'b', start: day(3), end: day(10) },
        ],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert — p spans the union of its descendants; leaves are not summarized.
    expect(spans.get('p')).toMatchObject({ start: day(0), end: day(10) })
    expect(spans.has('a')).toBe(false)
    expect(spans.has('b')).toBe(false)
  })

  it('does not summarize a leaf node', () => {
    // Arrange — a lone leaf with its own range.
    const roots: Node[] = [{ id: 'a', start: day(0), end: day(5) }]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert
    expect(spans.size).toBe(0)
  })

  it('includes the parent’s own range in the rolled-up span', () => {
    // Arrange — parent has its own range that extends beyond its child.
    const roots: Node[] = [
      {
        id: 'p',
        start: day(0),
        end: day(20),
        children: [{ id: 'a', start: day(5), end: day(8) }],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert — span is the union of p's own range and its child's.
    expect(spans.get('p')).toMatchObject({ start: day(0), end: day(20) })
  })

  it('rolls up through multiple levels', () => {
    // Arrange — grandparent → parent → leaf.
    const roots: Node[] = [
      {
        id: 'gp',
        children: [
          {
            id: 'p',
            children: [{ id: 'leaf', start: day(2), end: day(9) }],
          },
        ],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert — both gp and p bracket the leaf.
    expect(spans.get('gp')).toMatchObject({ start: day(2), end: day(9) })
    expect(spans.get('p')).toMatchObject({ start: day(2), end: day(9) })
  })

  it('leaves progress undefined when no descendant carries progress', () => {
    // Arrange
    const roots: Node[] = [
      { id: 'p', children: [{ id: 'a', start: day(0), end: day(5) }] },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert — progress is not defaulted to 0.
    expect(spans.get('p')?.progress).toBeUndefined()
  })

  it('duration-weights progress across descendants that have it', () => {
    // Arrange — 20-day 100% and 10-day 40% → (100*20 + 40*10) / 30 = 80.
    const roots: Node[] = [
      {
        id: 'p',
        children: [
          { id: 'a', start: day(0), end: day(20), progress: 100 },
          { id: 'b', start: day(0), end: day(10), progress: 40 },
        ],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert
    expect(spans.get('p')?.progress).toBeCloseTo(80)
  })

  it('averages only over descendants that carry progress', () => {
    // Arrange — one 10-day 60% range and one 10-day range with NO progress.
    const roots: Node[] = [
      {
        id: 'p',
        children: [
          { id: 'a', start: day(0), end: day(10), progress: 60 },
          { id: 'b', start: day(0), end: day(10) },
        ],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert
    expect(spans.get('p')?.progress).toBeCloseTo(60)
  })

  it('ignores nodes without a range (e.g. milestones handled elsewhere)', () => {
    // Arrange — a child with no start/end contributes nothing to the span.
    const roots: Node[] = [
      {
        id: 'p',
        children: [
          { id: 'a', start: day(2), end: day(8) },
          { id: 'noRange' },
        ],
      },
    ]
    // Act
    const spans = rollupSummaries(roots, accessors)
    // Assert
    expect(spans.get('p')).toMatchObject({ start: day(2), end: day(8) })
  })
})
