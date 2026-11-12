// @vitest-environment jsdom
import { Tooltip } from '@antv/component'
// eslint-disable-next-line no-restricted-imports -- the check plots runs on config functions, imported to run it
import { isValidElement } from '@ant-design/plots/lib/util/is-valid-element'
import { renderChartTooltip, withTextTooltip } from './chart-tooltip'

const MARKUP = '<img src="x" onerror="alert(1)">'

describe('renderChartTooltip', () => {
  it('sets the title, names and values as text', () => {
    // Arrange
    const content = {
      title: MARKUP,
      items: [{ name: MARKUP, value: MARKUP, color: '#1677ff' }],
    }

    // Act
    const root = renderChartTooltip(undefined, content)

    // Assert
    expect(root.querySelector('img')).toBeNull()
    expect(root.querySelector('.g2-tooltip-title')!.textContent).toBe(MARKUP)
    expect(
      root.querySelector('.g2-tooltip-list-item-name-label')!.textContent,
    ).toBe(MARKUP)
    expect(root.querySelector('.g2-tooltip-list-item-value')!.textContent).toBe(
      MARKUP,
    )
  })

  it('renders a row per item with its marker color, and numbers as text', () => {
    // Arrange
    const content = {
      title: 'Objectives',
      items: [
        { name: 'On Track', value: 3, color: 'rgb(22, 119, 255)' },
        { name: 'Missed', value: 0, color: 'rgb(255, 77, 79)' },
      ],
    }

    // Act
    const root = renderChartTooltip(undefined, content)

    // Assert
    const rows = root.querySelectorAll<HTMLElement>('.g2-tooltip-list-item')
    expect(rows).toHaveLength(2)
    expect(rows[1].dataset.index).toBe('1')
    expect(
      rows[1].querySelector('.g2-tooltip-list-item-value')!.textContent,
    ).toBe('0')
    expect(
      rows[0].querySelector<HTMLElement>('.g2-tooltip-list-item-marker')!.style
        .background,
    ).toBe('rgb(22, 119, 255)')
  })

  it('shows an empty value other than 0 as blank, as G2 does', () => {
    // Arrange
    const content = {
      items: [
        { name: 'A', value: undefined },
        { name: 'B', value: null },
        { name: 'C', value: false },
        { name: 'D', value: Number.NaN },
      ],
    }

    // Act
    const root = renderChartTooltip(undefined, content)

    // Assert
    const values = [
      ...root.querySelectorAll('.g2-tooltip-list-item-value'),
    ].map((el) => el.textContent)
    expect(values).toEqual(['', '', '', ''])
  })

  it('drops a marker color that is not a color', () => {
    // Arrange
    const content = {
      items: [{ name: 'A', value: 1, color: 'red" onmouseover="alert(1)' }],
    }

    // Act
    const root = renderChartTooltip(undefined, content)

    // Assert
    const marker = root.querySelector<HTMLElement>(
      '.g2-tooltip-list-item-marker',
    )!
    expect(marker.style.background).toBe('')
  })

  it('leaves out the title when there is none', () => {
    // Arrange
    const content = { items: [] }

    // Act
    const root = renderChartTooltip(undefined, content)

    // Assert
    expect(root.querySelector('.g2-tooltip-title')).toBeNull()
  })

  it('keeps markup inert inside the G2 tooltip component', () => {
    // Arrange
    const tooltip = new Tooltip({
      style: {
        container: { x: 0, y: 0 },
        bounding: { x: 0, y: 0, width: 800, height: 600 },
        template: { prefixCls: 'g2-' },
      },
    })
    const data = { title: MARKUP, items: [{ name: MARKUP, value: MARKUP }] }

    // Act
    tooltip.update({
      title: data.title,
      data: data.items,
      content: renderChartTooltip(undefined, data),
    })

    // Assert
    const element = tooltip.HTMLTooltipElement
    expect(element.querySelector('img')).toBeNull()
    expect(element.textContent).toContain(MARKUP)
  })

  it('is not taken for a React component by @ant-design/plots', () => {
    // Arrange
    const { render } = (withTextTooltip({}) as any).interaction.tooltip

    // Act
    const looksLikeReact = isValidElement(String(render))

    // Assert
    expect(looksLikeReact).toBe(false)
  })
})

describe('withTextTooltip', () => {
  it('adds the render to a config with no interaction', () => {
    // Arrange
    const input = { data: [] }

    // Act
    const config = withTextTooltip(input) as any

    // Assert
    expect(config.interaction.tooltip.render).toBe(renderChartTooltip)
  })

  it('keeps the other tooltip interaction options', () => {
    // Arrange
    const input = {
      interaction: { tooltip: { mount: 'body' }, elementHighlight: true },
    }

    // Act
    const config = withTextTooltip(input) as any

    // Assert
    expect(config.interaction).toEqual({
      tooltip: { mount: 'body', render: renderChartTooltip },
      elementHighlight: true,
    })
  })

  it('replaces a render the config supplies', () => {
    // Arrange
    const input = { interaction: { tooltip: { render: () => MARKUP } } }

    // Act
    const config = withTextTooltip(input) as any

    // Assert
    expect(config.interaction.tooltip.render).toBe(renderChartTooltip)
  })

  it("replaces a render on a child's interaction", () => {
    // Arrange
    const input = {
      children: [
        { type: 'line', interaction: { tooltip: { render: () => MARKUP } } },
        { type: 'point' },
      ],
    }

    // Act
    const config = withTextTooltip(input) as any

    // Assert
    expect(config.children[0].interaction.tooltip.render).toBe(
      renderChartTooltip,
    )
    expect(config.children[1]).toEqual({ type: 'point' })
  })

  it('leaves a tooltip that is turned off', () => {
    // Arrange
    const input = { interaction: { tooltip: false } }

    // Act
    const config = withTextTooltip(input)

    // Assert
    expect(config).toEqual(input)
  })
})
