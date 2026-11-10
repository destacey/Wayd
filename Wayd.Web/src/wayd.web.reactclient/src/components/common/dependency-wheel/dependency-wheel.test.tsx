import { DependencyHealth } from '@/src/components/types'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import DependencyWheel from './dependency-wheel'
import { DependencyWheelData } from './dependency-wheel-data'

const health = (
  healthy: number,
  atRisk: number,
  unhealthy: number,
): Record<DependencyHealth, number> => ({
  [DependencyHealth.Healthy]: healthy,
  [DependencyHealth.AtRisk]: atRisk,
  [DependencyHealth.Unhealthy]: unhealthy,
  [DependencyHealth.Unknown]: 0,
  [DependencyHealth.Closed]: 0,
})

const data: DependencyWheelData = {
  groups: [
    {
      key: 'a',
      name: 'Atlas',
      label: 'ATL',
      outgoing: 2,
      incoming: 1,
      healthCounts: health(2, 0, 1),
      partners: [{ key: 'b', label: 'BCN', count: 3 }],
    },
    {
      key: 'b',
      name: 'Beacon',
      label: 'BCN',
      outgoing: 1,
      incoming: 2,
      healthCounts: health(2, 0, 1),
      partners: [{ key: 'a', label: 'ATL', count: 3 }],
    },
  ],
  matrix: [
    [0, 2],
    [1, 0],
  ],
  flows: [
    {
      sourceKey: 'a',
      targetKey: 'b',
      count: 2,
      health: DependencyHealth.Unhealthy,
      healthCounts: health(1, 0, 1),
      states: [
        { id: 2, name: 'In Progress', count: 1 },
        { id: 4, name: 'Removed', count: 1 },
      ],
    },
    {
      sourceKey: 'b',
      targetKey: 'a',
      count: 1,
      health: DependencyHealth.Healthy,
      healthCounts: health(1, 0, 0),
      states: [{ id: 1, name: 'To Do', count: 1 }],
    },
  ],
  undrawn: 0,
}

const renderWheel = (
  selection: Parameters<typeof DependencyWheel>[0]['selection'] = null,
) => {
  const onSelectionChange = vi.fn()
  render(
    <DependencyWheel
      data={data}
      selection={selection}
      onSelectionChange={onSelectionChange}
      exportFileName="wheel"
    />,
  )
  return { onSelectionChange }
}

describe('DependencyWheel', () => {
  it('draws an arc per team and a ribbon per direction, each naming what it shows', () => {
    // Act
    renderWheel()

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Atlas: predecessor in 2 dependencies, successor in 1 dependency',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', {
        name: 'Atlas delivers to Beacon: 2 dependencies, worst health Unhealthy',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', {
        name: 'Beacon delivers to Atlas: 1 dependency, worst health Healthy',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('ATL')).toBeInTheDocument()
    expect(screen.getByText('BCN')).toBeInTheDocument()
  })

  it('draws each arc and ribbon as a path', () => {
    // Act
    renderWheel()

    // Assert
    expect(
      screen.getByRole('button', { name: /^Atlas:/ }).getAttribute('d'),
    ).toMatch(/^M/)
    expect(
      screen
        .getByRole('button', { name: /^Atlas delivers to Beacon/ })
        .getAttribute('d'),
    ).toMatch(/^M/)
  })

  it('keeps ribbons out of the tab order until one of their teams is picked', () => {
    // Act
    renderWheel({ kind: 'group', key: 'a' })

    // Assert
    expect(screen.getByRole('button', { name: /^Atlas:/ })).toHaveAttribute(
      'tabindex',
      '0',
    )
    expect(
      screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }),
    ).toHaveAttribute('tabindex', '0')
  })

  it('leaves every ribbon out of the tab order when nothing is picked', () => {
    // Act
    renderWheel()

    // Assert
    expect(
      screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }),
    ).toHaveAttribute('tabindex', '-1')
    expect(
      screen.getByRole('button', { name: /^Beacon delivers to Atlas/ }),
    ).toHaveAttribute('tabindex', '-1')
  })

  it('shows a team summary while its arc is hovered', async () => {
    // Arrange
    renderWheel()

    // Act
    await userEvent.hover(screen.getByRole('button', { name: /^Atlas:/ }))

    // Assert
    expect(screen.getByText('Waiting on others').nextSibling).toHaveTextContent(
      '1',
    )
    expect(
      screen.getByText('Others waiting on it').nextSibling,
    ).toHaveTextContent('2')
    expect(screen.getByText('2 Healthy')).toBeInTheDocument()
    expect(screen.getByText('1 Unhealthy')).toBeInTheDocument()
    expect(screen.getByText('Most linked: BCN 3')).toBeInTheDocument()
  })

  it('shows a ribbon summary while it is focused, and hides it on blur', async () => {
    // Arrange
    renderWheel()
    const ribbon = screen.getByRole('button', {
      name: /^Atlas delivers to Beacon/,
    })

    // Act
    await act(async () => ribbon.focus())
    const shown = [
      screen.queryByText('BCN waits on ATL: 2 dependencies')?.textContent,
      screen.queryByText('1 In Progress · 1 Removed')?.textContent,
    ]
    await act(async () => ribbon.blur())

    // Assert
    expect(shown).toEqual([
      'BCN waits on ATL: 2 dependencies',
      '1 In Progress · 1 Removed',
    ])
    expect(
      screen.queryByText('1 In Progress · 1 Removed'),
    ).not.toBeInTheDocument()
  })

  it('picks a team when its arc is clicked', async () => {
    // Arrange
    const { onSelectionChange } = renderWheel()

    // Act
    await userEvent.click(screen.getByRole('button', { name: /^Beacon:/ }))

    // Assert
    expect(onSelectionChange).toHaveBeenCalledWith({ kind: 'group', key: 'b' })
  })

  it('picks a directed pair when its ribbon is activated from the keyboard', async () => {
    // Arrange
    const { onSelectionChange } = renderWheel()
    screen.getByRole('button', { name: /^Atlas delivers to Beacon/ }).focus()

    // Act
    await userEvent.keyboard('{Enter}')

    // Assert
    expect(onSelectionChange).toHaveBeenCalledWith({
      kind: 'flow',
      sourceKey: 'a',
      targetKey: 'b',
    })
  })

  it('clears the pick when the picked arc is clicked again', async () => {
    // Arrange
    const { onSelectionChange } = renderWheel({ kind: 'group', key: 'a' })
    const atlas = screen.getByRole('button', { name: /^Atlas:/ })

    // Act
    await userEvent.click(atlas)

    // Assert
    expect(atlas).toHaveAttribute('aria-pressed', 'true')
    expect(onSelectionChange).toHaveBeenCalledWith(null)
  })
})
