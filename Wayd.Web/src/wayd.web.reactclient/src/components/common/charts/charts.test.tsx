import { render, screen } from '@testing-library/react'
import { lazy, Suspense } from 'react'
import { renderChartTooltip } from './chart-tooltip'
import { Area, Column, Line, Pie, Radar } from './charts'

// Resolves the loader through React.lazy, so the module's own wrapper renders rather than a stand-in.
vi.mock(import('next/dynamic'), () => ({
  default: ((loader: () => Promise<any>) =>
    lazy(() => loader().then((Component) => ({ default: Component })))) as any,
}))

const chartProps = vi.hoisted(() => vi.fn())

// Stand-ins for the five charts the module wraps; the real package draws to a canvas jsdom lacks.
vi.mock(
  import('@ant-design/charts'),
  () =>
    Object.fromEntries(
      ['Area', 'Column', 'Line', 'Pie', 'Radar'].map((name) => {
        const FakeChart = (props: any) => {
          chartProps(name, props)
          return <div data-testid={name} />
        }
        return [name, FakeChart]
      }),
    ) as unknown as typeof import('@ant-design/charts'),
)

describe('charts', () => {
  it.each([
    ['Area', Area],
    ['Column', Column],
    ['Line', Line],
    ['Pie', Pie],
    ['Radar', Radar],
  ])('%s renders its tooltip with renderChartTooltip', async (name, Chart) => {
    // Arrange
    const config = {
      data: [{ type: 'A', count: 1 }],
      interaction: { tooltip: { mount: 'body' } },
    }

    // Act
    render(
      <Suspense>
        <Chart {...config} />
      </Suspense>,
    )
    await screen.findByTestId(name)

    // Assert
    const [, props] = chartProps.mock.calls.find(([n]) => n === name)!
    expect(props.data).toEqual(config.data)
    expect(props.interaction.tooltip).toEqual({
      mount: 'body',
      render: renderChartTooltip,
    })
  })
})
