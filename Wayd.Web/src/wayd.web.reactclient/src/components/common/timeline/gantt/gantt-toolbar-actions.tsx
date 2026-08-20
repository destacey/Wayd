'use client'

// gantt-toolbar-actions.tsx — the show/hide + zoom controls every Gantt pane
// puts in its grid's actionsSlot. Shared so the roadmap and the project plan
// present the same affordances (and the same icons/labels) rather than each
// hand-rolling the button row.

import {
  BarChartOutlined,
  UndoOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
} from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import { ZOOM_STEP } from './use-gantt-pane'
import type { UseGanttZoom } from './use-gantt-zoom'

export interface GanttToolbarActionsProps {
  /** Whether the chart pane is currently shown. */
  visible: boolean
  /** Toggle the chart pane. */
  onToggle: () => void
  /** Zoom state from useGanttZoom; zoom buttons render only when visible. */
  zoom: UseGanttZoom
  /** Overrides the toggle tooltip/aria noun (default: "Gantt chart"). */
  label?: string
}

export function GanttToolbarActions({
  visible,
  onToggle,
  zoom,
  label = 'Gantt chart',
}: GanttToolbarActionsProps) {
  return (
    <>
      <Tooltip title={visible ? `Hide ${label}` : `Show ${label}`}>
        <Button
          type="text"
          shape="circle"
          icon={
            <BarChartOutlined
              // Mirror (invert) + rotate -90°, statically, so the bars read
              // left-anchored like Gantt rows. Both transforms in one style
              // so they compose (the `rotate` prop can't combine with flip).
              style={{ transform: 'scaleX(-1) rotate(-90deg)' }}
            />
          }
          onClick={onToggle}
          aria-pressed={visible}
          aria-label={`Toggle ${label}`}
          style={visible ? { color: 'var(--ant-color-primary)' } : undefined}
        />
      </Tooltip>
      {visible && (
        <>
          <Tooltip title="Zoom out">
            <Button
              type="text"
              shape="circle"
              icon={<ZoomOutOutlined />}
              onClick={() => zoom.zoomBy(1 / ZOOM_STEP)}
              disabled={!zoom.canZoomOut}
              aria-label="Zoom out"
            />
          </Tooltip>
          <Tooltip title="Zoom in">
            <Button
              type="text"
              shape="circle"
              icon={<ZoomInOutlined />}
              onClick={() => zoom.zoomBy(ZOOM_STEP)}
              disabled={!zoom.canZoomIn}
              aria-label="Zoom in"
            />
          </Tooltip>
          <Tooltip title="Reset zoom">
            <Button
              type="text"
              shape="circle"
              icon={<UndoOutlined />}
              onClick={zoom.resetZoom}
              disabled={!zoom.isZoomed}
              aria-label="Reset zoom"
            />
          </Tooltip>
        </>
      )}
    </>
  )
}
