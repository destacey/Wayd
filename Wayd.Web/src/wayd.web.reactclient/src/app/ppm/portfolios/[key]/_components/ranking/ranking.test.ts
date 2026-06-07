import { buildMoveRanksPayload, RankableRow } from './ranking'

const row = (id: string, rank: number | null = null): RankableRow => ({
  id,
  rank,
})

describe('buildMoveRanksPayload', () => {
  it('returns null defensively when no row is ranked (no anchor to position against)', () => {
    // Arrange
    const rows = [row('a'), row('b'), row('c')]

    // Act
    const payload = buildMoveRanksPayload(rows, [1])

    // Assert
    expect(payload).toBeNull()
  })

  it('places a moved row between two ranked anchors', () => {
    // Arrange — moved 'm' dropped between ranked 'a'(1000) and 'b'(2000).
    const rows = [row('a', 1000), row('m', 1500), row('b', 2000)]

    // Act
    const payload = buildMoveRanksPayload(rows, [1])

    // Assert
    expect(payload).toEqual({
      projectIds: ['m'],
      afterProjectId: 'a',
      beforeProjectId: 'b',
    })
  })

  it('omits the after anchor when dropped at the very top', () => {
    // Arrange — moved 'm' is now first, above ranked 'a'.
    const rows = [row('m', 500), row('a', 1000)]

    // Act
    const payload = buildMoveRanksPayload(rows, [0])

    // Assert
    expect(payload).toEqual({
      projectIds: ['m'],
      afterProjectId: undefined,
      beforeProjectId: 'a',
    })
  })

  it('omits the before anchor when the row below is unranked', () => {
    // Arrange — moved 'm' below ranked 'a'; 'u' below it is unranked.
    const rows = [row('a', 1000), row('m', 1500), row('u', null)]

    // Act
    const payload = buildMoveRanksPayload(rows, [1])

    // Assert
    expect(payload).toEqual({
      projectIds: ['m'],
      afterProjectId: 'a',
      beforeProjectId: undefined,
    })
  })

  it('folds unranked rows between the anchor and the drop into the batch (survives refresh)', () => {
    // Arrange — first drag into a mostly-unranked list: anchor 'a'(1000), then unranked u1, u2, then
    // the moved 'm' dropped below them. u1 and u2 must be baked in so the order survives refetch.
    const rows = [row('a', 1000), row('u1'), row('u2'), row('m'), row('z', 2000)]

    // Act
    const payload = buildMoveRanksPayload(rows, [3])

    // Assert
    expect(payload).toEqual({
      projectIds: ['u1', 'u2', 'm'],
      afterProjectId: 'a',
      beforeProjectId: 'z',
    })
  })

  it('keeps a multi-select batch contiguous and ordered', () => {
    // Arrange — x,y dragged together between ranked a and b.
    const rows = [row('a', 1000), row('x', 1200), row('y', 1400), row('b', 2000)]

    // Act
    const payload = buildMoveRanksPayload(rows, [1, 2])

    // Assert
    expect(payload).toEqual({
      projectIds: ['x', 'y'],
      afterProjectId: 'a',
      beforeProjectId: 'b',
    })
  })

  it('returns null for an empty move', () => {
    // Arrange
    const rows = [row('a', 1000)]

    // Act / Assert
    expect(buildMoveRanksPayload(rows, [])).toBeNull()
  })
})
