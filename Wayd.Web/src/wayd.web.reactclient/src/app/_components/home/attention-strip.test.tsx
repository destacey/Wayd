import { render, screen } from '@testing-library/react'
import { FC } from 'react'
import AttentionStrip from './attention-strip'
import AttentionTile from './attention-tile'
import { attentionTiles, HomeTileProps } from './widget-registry'

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
    title: id,
    widgetId: `widget-${id}`,
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

  it('gives a muted tile’s slot to the next, and never renders it', () => {
    // Arrange
    const loads = vi.fn()
    const Muted: FC<HomeTileProps> = ({ title }) => {
      loads()
      return counting(title, 9)({})
    }
    const columns = {
      main: [
        {
          id: 'widget',
          title: 'Widget',
          description: 'A widget.',
          defaultColumn: 'main' as const,
          defaultOrder: 10,
          requires: {},
          Component: () => null,
          tiles: [
            { id: 'muted', title: 'Muted', priority: 100, Tile: Muted },
            ...tiles(['A', 1], ['B', 2], ['C', 3], ['D', 4]),
          ],
        },
      ],
      side: [],
    }

    // Act
    render(
      <AttentionStrip tiles={attentionTiles(columns, new Set(['muted']))} />,
    )

    // Assert
    const shown = screen
      .getAllByRole('button')
      .map((b) => b.getAttribute('aria-label'))
    expect(shown).toEqual(['A: 1', 'B: 2', 'C: 3', 'D: 4'])
    expect(loads).not.toHaveBeenCalled()
  })

  it('names each tile with the title its definition gives', () => {
    // Arrange
    const Titled: FC<HomeTileProps> = ({ title }) => counting(title, 1)({})

    // Act
    render(
      <AttentionStrip
        tiles={[
          {
            id: 'titled',
            title: 'From the registry',
            widgetId: 'w',
            priority: 1,
            Tile: Titled,
          },
        ]}
      />,
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'From the registry: 1' }),
    ).toBeInTheDocument()
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
    title: id,
    widgetId: `widget-${id}`,
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

  it('forgets the count of a tile that leaves the strip, so it returns waiting for a fresh one', () => {
    // Arrange
    const others = [
      tile('B', 90, 1, false),
      tile('C', 80, 1, false),
      tile('D', 70, 1, false),
      tile('E', 60, 1, false),
    ]
    const { rerender } = render(
      <AttentionStrip tiles={[tile('A', 100, 0, false), ...others]} />,
    )
    rerender(<AttentionStrip tiles={others} />)
    const NotYetReported: FC = () => <span>A pending</span>

    // Act
    rerender(
      <AttentionStrip
        tiles={[
          {
            id: 'A',
            title: 'A',
            widgetId: 'widget-A',
            priority: 100,
            Tile: NotYetReported,
          },
          ...others,
        ]}
      />,
    )

    // Assert
    expect(screen.getByText('A pending')).toBeVisible()
    expect(
      screen.queryByRole('button', { name: 'E: 1' }),
    ).not.toBeInTheDocument()
  })

  it('hands each tile the settings of the widget it belongs to', () => {
    // Arrange
    const Reading: FC<HomeTileProps> = ({ settings }) => (
      <AttentionTile
        title="Reading"
        value={Number(settings?.rows ?? 0)}
        tooltip="Reading tooltip"
        href="/somewhere"
        alert="warning"
        loading={false}
      />
    )

    // Act
    render(
      <AttentionStrip
        tiles={[
          {
            id: 'reading',
            title: 'Reading',
            widgetId: 'mine',
            priority: 1,
            Tile: Reading,
          },
        ]}
        settings={{ mine: { rows: '3' }, other: { rows: '9' } }}
      />,
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'Reading: 3' }),
    ).toBeInTheDocument()
  })
})
