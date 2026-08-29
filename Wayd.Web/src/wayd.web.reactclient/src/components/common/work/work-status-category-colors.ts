import type { GlobalToken } from 'antd'
import {
  getSemanticChartColor,
  getWorkStatusCategoryColor,
  softenChartColor,
} from '@/src/utils'

/** The status categories a work item passes through, in lifecycle order. */
export const WORK_STATUS_CATEGORIES = [
  'Proposed',
  'Active',
  'Done',
  'Removed',
] as const

type ChartColorTokens = Pick<
  GlobalToken,
  | 'colorInfo'
  | 'colorSuccess'
  | 'colorError'
  | 'colorWarning'
  | 'colorTextSecondary'
  | 'colorBgContainer'
>

/**
 * A G2 `scale.color` pinning each work status category to the color its tag
 * uses — Proposed grey, Active blue, Done green.
 *
 * Pass this to every chart plotting these categories: without an explicit
 * scale G2 assigns colors by series order, so two charts on one page give the
 * same category different colors.
 *
 * The domain must stay fixed. Handing G2 only the categories present
 * re-indexes the palette whenever one is empty, drawing Done in Active's
 * color. It also keeps absent categories in the legend.
 */
export const getWorkStatusCategoryColorScale = (token: ChartColorTokens) => ({
  domain: [...WORK_STATUS_CATEGORIES],
  range: WORK_STATUS_CATEGORIES.map((category) =>
    softenChartColor(
      getSemanticChartColor(getWorkStatusCategoryColor(category), token),
      token.colorBgContainer,
    ),
  ),
})
