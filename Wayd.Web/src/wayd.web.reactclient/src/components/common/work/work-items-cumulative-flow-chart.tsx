'use client'

import dynamic from 'next/dynamic'
const Area = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Area) as any,
  { ssr: false },
)
import { AreaConfig } from '@ant-design/charts'
import useTheme from '../../contexts/theme'
import { ChartCard } from '../metrics'
import { useChartRemountOnResize } from '@/src/hooks'
import { WorkItemProgressDailyRollupDto } from '@/src/services/wayd-api'
import WaydEmpty from '../wayd-empty'
import { getWorkStatusCategoryColorScale } from './work-status-category-colors'
import { theme } from 'antd'
import dayjs from 'dayjs'

export interface WorkItemsCumulativeFlowChartProps {
  workItems: WorkItemProgressDailyRollupDto[]
  isLoading: boolean
  /**
   * Drops the card border and title — for a container that already names the
   * chart, such as a modal whose own title says the same thing.
   */
  embedded?: boolean
}

const WorkItemsCumulativeFlowChart = (
  props: WorkItemsCumulativeFlowChartProps,
) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()
  const { ref, renderKey } = useChartRemountOnResize()

  // Derive chart data from work items
  const data = (() => {
    if (!props.workItems) return []

    const workItems = props.workItems

    const proposedData = workItems.map((item) => ({
      date: dayjs(item.date).toDate(),
      category: 'Proposed',
      value: item.proposed,
    }))

    const activeData = workItems.map((item) => ({
      date: dayjs(item.date).toDate(),
      category: 'Active',
      value: item.active,
    }))

    const doneData = workItems.map((item) => ({
      date: dayjs(item.date).toDate(),
      category: 'Done',
      value: item.done,
    }))

    return [...doneData, ...activeData, ...proposedData]
  })()

  const config = {
    // No `title`: `ChartCard` heads the chart, and G2 would draw a second one.
    theme: antDesignChartsTheme,
    // Without this the chart keeps G2's fixed default width and overflows its
    // card. Pairs with `useChartRemountOnResize` — see that hook for why
    // `autoFit` alone is not enough.
    autoFit: true,
    data: data,
    xField: 'date',
    yField: 'value',
    //seriesField: 'category', // not sure when to use seriesField vs colorField
    colorField: 'category',
    // Shared with the progress pie, so a category means the same color on both.
    scale: { color: getWorkStatusCategoryColorScale(token) },
    legend: {
      color: { layout: { justifyContent: 'center' }, itemMarker: 'square' },
    },
    stack: true,
    // style: {
    //   fill: (data) => {
    //     if (data[0].category === 'Done') {
    //       return '#49aa19' // 52c41a
    //     }
    //     if (data[0].category === 'Active') {
    //       return '#1668dc' // 1677ff
    //     }
    //     if (data[0].category === 'Proposed') {
    //       return '#f5f5f5'
    //     }
    //     return '#FFC107'
    //   },
    // },
    //stackField: 'category',
    // stack: {
    //   field: 'order',
    //   reverse: false,
    //   // orderBy: (a, b) => {
    //   //   const order = ['Done', 'Active', 'Proposed']
    //   //   return order.indexOf(a) - order.indexOf(b)
    //   // },
    // },
    //shapeField: 'smooth',
    // stack: {
    //   orderBy: 'total',
    //   reverse: true,
    // },

    // update the tooltip to show the date with this format 'MMM D, YYYY'
  } as AreaConfig

  // The ref must stay on one wrapper across every state, or the observer
  // re-attaches mid-load and first measures a spinner's width.
  return (
    <ChartCard
      title={props.embedded ? undefined : 'Cumulative Flow'}
      loading={props.isLoading}
      embedded={props.embedded}
    >
      <div ref={ref}>
        {data.length === 0 ? (
          <WaydEmpty message="No work item data to display" />
        ) : (
          <Area key={renderKey} {...config} />
        )}
      </div>
    </ChartCard>
  )
}

export default WorkItemsCumulativeFlowChart
