import { render, screen } from '@testing-library/react'
import { FC } from 'react'
import AttentionStrip from './attention-strip'
import AttentionTile from './attention-tile'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

const counting = (
  title: string,
  value: number | undefined,
  loading = false,
): FC => {
  const Counting: FC = () => (
    <AttentionTile
      title={title}
      value={value}
      tooltip={`${title} tooltip`}
      href="/somewhere"
      alert="warning"
      loading={loading}
    />
  )
  return Counting
}

const tiles = (...defs: [string, number | undefined][]) =>
  defs.map(([id, value], i) => ({
    id,
    priority: 100 - i,
    Tile: counting(id, value),
  }))

describe('AttentionStrip', () => {
  it('gives a zero’s slot to the next tile, up to four', () => {
    // Arrange / Act
    render(
      <AttentionStrip
        tiles={tiles(
          ['A', 1],
          ['B', 0],
          ['C', 2],
          ['D', 3],
          ['E', 4],
          ['F', 5],
        )}
      />,
    )

    // Assert
    const shown = screen
      .getAllByRole('button')
      .map((b) => b.getAttribute('aria-label'))
    expect(shown).toEqual(['A: 1', 'C: 2', 'D: 3', 'E: 4'])
  })

  it('is hidden when every count is zero', () => {
    // Arrange / Act
    render(<AttentionStrip tiles={tiles(['A', 0], ['B', 0])} />)

    // Assert
    expect(
      screen.queryByRole('region', { name: 'Needs attention' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('keeps a tile whose count failed', () => {
    // Arrange / Act
    render(<AttentionStrip tiles={tiles(['A', 0], ['B', undefined])} />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'B: unavailable' }),
    ).toBeInTheDocument()
  })
})

describe('AttentionStrip as counts arrive', () => {
  const tile = (
    id: string,
    priority: number,
    value: number | undefined,
    loading: boolean,
  ) => ({
    id,
    priority,
    Tile: counting(id, value, loading),
  })

  it('holds a slot for a loading tile, then gives it up when the count is zero', () => {
    // Arrange
    const others = [
      tile('B', 90, 1, false),
      tile('C', 80, 1, false),
      tile('D', 70, 1, false),
      tile('E', 60, 1, false),
    ]
    const { rerender } = render(
      <AttentionStrip tiles={[tile('A', 100, undefined, true), ...others]} />,
    )
    expect(
      screen.queryByRole('button', { name: 'E: 1' }),
    ).not.toBeInTheDocument()

    // Act
    rerender(<AttentionStrip tiles={[tile('A', 100, 0, false), ...others]} />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'A: 0' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'E: 1' })).toBeInTheDocument()
  })

  it('comes back when a hidden tile’s count rises above zero', () => {
    // Arrange
    const { rerender } = render(
      <AttentionStrip tiles={[tile('A', 100, 0, false)]} />,
    )
    expect(screen.queryByRole('button')).not.toBeInTheDocument()

    // Act
    rerender(<AttentionStrip tiles={[tile('A', 100, 2, false)]} />)

    // Assert
    expect(
      screen.getByRole('region', { name: 'Needs attention' }),
    ).toBeVisible()
    expect(screen.getByRole('button', { name: 'A: 2' })).toBeInTheDocument()
  })
})
