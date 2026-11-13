import { render } from '@testing-library/react'
import { ReactElement } from 'react'
import type { Mock } from 'vitest'
import {
  WidgetDisplay,
  WidgetDisplayContext,
  WidgetState,
  WidgetStateReport,
} from './widget-display'
import { EmptyDisplay } from './widget-settings'

/** The page's side of a widget's display: its empty setting and a spy for its reports. */
export const widgetDisplay = (
  emptyDisplay: EmptyDisplay = 'explain',
  revealed = false,
): WidgetDisplay & { onReport: Mock<(report: WidgetStateReport) => void> } => ({
  widgetId: 'widget',
  emptyDisplay,
  revealed,
  onReport: vi.fn<(report: WidgetStateReport) => void>(),
})

/**
 * Renders a widget as the page would and returns, with the render, the state
 * it last reported.
 */
export const renderReportingWidget = (
  ui: ReactElement,
  display = widgetDisplay(),
) => {
  const result = render(
    <WidgetDisplayContext value={display}>{ui}</WidgetDisplayContext>,
  )
  const reportedState = (): WidgetState | undefined =>
    display.onReport.mock.calls
      .map(([report]) => report.state)
      .filter((state) => state !== undefined)
      .at(-1)
  return { ...result, reportedState }
}
