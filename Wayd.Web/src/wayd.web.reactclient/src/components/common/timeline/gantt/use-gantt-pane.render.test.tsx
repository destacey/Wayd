// The pane builds its axis through scale.tiers(), which needs REAL dayjs
// (.startOf/.add); the global mock only stubs format. Same reason as scale.test.ts.
jest.unmock('dayjs')

import { fireEvent, render, renderHook } from '@testing-library/react'
import { useGanttPane } from './use-gantt-pane'
import type { GanttAccessors } from './types'

const DAY = 86_400_000
const day = (n: number) => n * DAY

interface Node {
  id: string
  name: string
  start?: number
  end?: number
  progress?: number
  milestone?: boolean
  children?: Node[]
}

const accessors: GanttAccessors<Node> = {
  id: (n) => n.id,
  children: (n) => n.children,
  name: (n) => n.name,
  kind: (n) => (n.milestone ? 'milestone' : 'range'),
  range: (n) =>
    n.start != null && n.end != null ? [n.start, n.end] : undefined,
  progress: (n) => n.progress,
}

/** Render one row through the pane's renderRow and return its DOM. */
function renderRow(
  tree: Node[],
  node: Node,
  editable = false,
  onBarPointerDown?: jest.Mock,
) {
  const { result } = renderHook(() =>
    useGanttPane(tree, accessors, { editable, onBarPointerDown }),
  )
  return render(
    <div>
      {result.current.renderRow({ row: { original: node }, top: 0, height: 32 })}
    </div>,
  )
}

describe('useGanttPane', () => {
  it('renders a dated leaf as a positioned bar carrying its name', () => {
    // Arrange
    const task: Node = { id: 't1', name: 'Design', start: day(0), end: day(5) }
    // Act
    const { container } = renderRow([task], task)
    // Assert — one bar, labeled, with a non-zero width.
    const bar = container.querySelector('[class*="bar"]') as HTMLElement
    expect(bar).toBeTruthy()
    expect(bar.textContent).toContain('Design')
    expect(parseFloat(bar.style.width)).toBeGreaterThan(0)
  })

  it('renders a milestone as a diamond rather than a bar', () => {
    // Arrange — start === end, flagged as a milestone.
    const ms: Node = {
      id: 'm1',
      name: 'Go live',
      start: day(3),
      end: day(3),
      milestone: true,
    }
    // Act
    const { container } = renderRow([ms], ms)
    // Assert
    expect(container.querySelector('[class*="milestone"]')).toBeTruthy()
    expect(container.querySelector('[class*="barLabel"]')).toBeNull()
  })

  it('renders an undated parent as a summary bar spanning its children', () => {
    // Arrange — the stage has no dates; its tasks run day 0 → day 10.
    const stage: Node = {
      id: 's1',
      name: 'Build',
      children: [
        { id: 't1', name: 'A', start: day(0), end: day(4) },
        { id: 't2', name: 'B', start: day(6), end: day(10) },
      ],
    }
    // Act
    const { container } = renderRow([stage], stage)
    // Assert
    expect(container.querySelector('[class*="summaryBar"]')).toBeTruthy()
  })

  it('renders nothing for a row with no dates and no dated children', () => {
    // Arrange — an empty stage.
    const stage: Node = { id: 's1', name: 'Empty', children: [] }
    // Act
    const { container } = renderRow([stage], stage)
    // Assert
    expect(container.firstChild?.hasChildNodes()).toBe(false)
  })

  it('draws a progress fill proportional to a task’s progress', () => {
    // Arrange
    const task: Node = {
      id: 't1',
      name: 'Half done',
      start: day(0),
      end: day(10),
      progress: 50,
    }
    // Act
    const { container } = renderRow([task], task)
    // Assert
    const fill = container.querySelector(
      '[class*="progressFill"]',
    ) as HTMLElement
    expect(fill).toBeTruthy()
    expect(fill.style.width).toBe('50%')
  })

  it('omits the progress fill at zero progress', () => {
    // Arrange
    const task: Node = {
      id: 't1',
      name: 'Not started',
      start: day(0),
      end: day(10),
      progress: 0,
    }
    // Act
    const { container } = renderRow([task], task)
    // Assert — no empty sliver drawn on an unstarted bar.
    expect(container.querySelector('[class*="progressFill"]')).toBeNull()
  })

  it('adds resize handles only when the pane is editable', () => {
    // Arrange
    const task: Node = { id: 't1', name: 'Design', start: day(0), end: day(5) }
    // Act
    const readOnly = renderRow([task], task, false)
    // Assert — handles need both editable AND an onBarPointerDown, which this
    // render omits, so none appear.
    expect(readOnly.container.querySelector('[class*="handle"]')).toBeNull()
  })

  it('builds an axis header and gridlines for the domain', () => {
    // Arrange
    const task: Node = { id: 't1', name: 'Design', start: day(0), end: day(30) }
    // Act
    const { result } = renderHook(() => useGanttPane([task], accessors))
    const header = render(<div>{result.current.header}</div>)
    const bg = render(
      <div>{result.current.renderBackground({ totalHeight: 100 })}</div>,
    )
    // Assert
    expect(header.container.querySelector('[class*="axis"]')).toBeTruthy()
    expect(bg.container.querySelectorAll('[class*="gridline"]').length)
      .toBeGreaterThan(0)
  })

  it('exposes a drag scale and domain consistent with the axis', () => {
    // Arrange
    const task: Node = { id: 't1', name: 'Design', start: day(0), end: day(10) }
    // Act
    const { result } = renderHook(() => useGanttPane([task], accessors))
    // Assert — the drag hook clamps against these, so they must be sane.
    expect(result.current.pxPerMs).toBeGreaterThan(0)
    expect(result.current.domainMax).toBeGreaterThan(result.current.domainMin)
  })

  it('makes a milestone draggable when the pane is editable', () => {
    // Arrange
    const onBarPointerDown = jest.fn()
    const ms: Node = {
      id: 'm1',
      name: 'Go live',
      start: day(3),
      end: day(3),
      milestone: true,
    }
    // Act
    const { container } = renderRow([ms], ms, true, onBarPointerDown)
    const diamond = container.querySelector(
      '[class*="milestone"]',
    ) as HTMLElement
    fireEvent.pointerDown(diamond)
    // Assert — a milestone has no width, so move is the only valid mode.
    expect(onBarPointerDown).toHaveBeenCalledTimes(1)
    expect(onBarPointerDown.mock.calls[0][2]).toBe('move')
    expect(onBarPointerDown.mock.calls[0][1]).toMatchObject({
      id: 'm1',
      start: day(3),
      end: day(3),
    })
  })

  it('leaves a milestone static when the pane is read-only', () => {
    // Arrange
    const onBarPointerDown = jest.fn()
    const ms: Node = {
      id: 'm1',
      name: 'Go live',
      start: day(3),
      end: day(3),
      milestone: true,
    }
    // Act
    const { container } = renderRow([ms], ms, false, onBarPointerDown)
    fireEvent.pointerDown(
      container.querySelector('[class*="milestone"]') as HTMLElement,
    )
    // Assert
    expect(onBarPointerDown).not.toHaveBeenCalled()
  })
})
