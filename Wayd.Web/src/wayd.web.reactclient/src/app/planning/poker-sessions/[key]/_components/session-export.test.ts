import { PokerRoundDto, PokerVoteDto } from '@/src/services/wayd-api'
import { buildExportRows, EXPORT_HEADERS } from './session-export'

const vote = (name: string, value: string): PokerVoteDto =>
  ({
    id: `${name}-${value}`,
    participant: { id: name, userName: name.toLowerCase(), name },
    value,
    submittedOn: new Date(),
  }) as PokerVoteDto

const round = (overrides: Partial<PokerRoundDto>): PokerRoundDto =>
  ({
    id: 'r1',
    status: 'Voting',
    order: 0,
    voteCount: 0,
    votes: [],
    ...overrides,
  }) as PokerRoundDto

describe('buildExportRows', () => {
  it('writes one row per round in round order, numbered from 1', () => {
    // Arrange
    const rounds = [
      round({ id: 'b', label: 'Second', order: 5 }),
      round({ id: 'a', label: 'First', order: 2 }),
    ]

    // Act
    const rows = buildExportRows(rounds)

    // Assert
    expect(rows.map((r) => [r[0], r[1]])).toEqual([
      [1, 'First'],
      [2, 'Second'],
    ])
    expect(rows[0]).toHaveLength(EXPORT_HEADERS.length)
  })

  it('writes the label, description, status and consensus', () => {
    // Arrange
    const rounds = [
      round({
        label: 'WI-1',
        description: 'Users can sign in',
        status: 'Accepted',
        consensusEstimate: '5',
      }),
    ]

    // Act
    const [row] = buildExportRows(rounds)

    // Assert
    expect(row.slice(1, 5)).toEqual([
      'WI-1',
      'Users can sign in',
      'Accepted',
      '5',
    ])
  })

  it('leaves blanks for a round with no label, description or consensus', () => {
    // Arrange
    const rounds = [round({})]

    // Act
    const [row] = buildExportRows(rounds)

    // Assert
    expect(row.slice(1, 5)).toEqual(['', '', 'Voting', ''])
  })

  it('lists revealed votes by voter name, case-insensitively', () => {
    // Arrange
    const rounds = [
      round({
        status: 'Revealed',
        voteCount: 3,
        votes: [vote('carol', '8'), vote('Alice', '5'), vote('Bob', '3')],
      }),
    ]

    // Act
    const [row] = buildExportRows(rounds)

    // Assert
    expect(row[5]).toBe(3)
    expect(row[6]).toBe('Alice: 5; Bob: 3; carol: 8')
  })

  it('leaves the votes out of a round still voting, keeping the count', () => {
    // Arrange
    const rounds = [
      round({
        status: 'Voting',
        voteCount: 2,
        votes: [vote('Alice', '5'), vote('Bob', '')],
      }),
    ]

    // Act
    const [row] = buildExportRows(rounds)

    // Assert
    expect(row[5]).toBe(2)
    expect(row[6]).toBe('')
  })
})
