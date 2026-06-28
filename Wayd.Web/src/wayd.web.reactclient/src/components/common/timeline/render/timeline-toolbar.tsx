'use client'

// timeline2/render/timeline-toolbar.tsx — the timeline's action bar, following
// the tree-grid toolbar pattern (WaydTooltip + text/circle icon buttons +
// leftSlot/rightSlot + dividers). leftSlot is where drill-through +/- controls
// will live; the built-in right-side actions are save-as-image and fullscreen.

import { ReactNode } from 'react'
import { Button, Dropdown, Popover, Space, Switch, Typography, type MenuProps } from 'antd'
import {
  FileImageOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  QuestionCircleOutlined,
  ReloadOutlined,
  SettingOutlined,
  UndoOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
} from '@ant-design/icons'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import styles from './timeline.module.css'

export interface TimelineToolbarProps {
  /** Left-aligned controls (e.g. future drill-through +/- buttons). */
  leftSlot?: ReactNode
  /** Extra right-aligned controls, placed before the built-in actions. */
  rightSlot?: ReactNode
  allowZoom?: boolean
  onZoomIn?: () => void
  onZoomOut?: () => void
  onResetView?: () => void
  canZoomIn?: boolean
  canZoomOut?: boolean
  /** Reset View is only meaningful once zoomed/panned away from the start. */
  canReset?: boolean
  allowSaveAsImage?: boolean
  onSaveAsImage?: () => void
  allowFullScreen?: boolean
  isFullScreen?: boolean
  onToggleFullScreen?: () => void
  /** Settings menu (gear) — shown when any setting is togglable. */
  allowSettings?: boolean
  showCurrentTime?: boolean
  onToggleCurrentTime?: (checked: boolean) => void
  showVerticalGridlines?: boolean
  onToggleVerticalGridlines?: (checked: boolean) => void
  showWeekends?: boolean
  onToggleWeekends?: (checked: boolean) => void
  isCompact?: boolean
  onToggleCompact?: (checked: boolean) => void
  /** Refresh action — shown (ReloadOutlined) only when provided, like WaydGrid. */
  onRefresh?: () => void
  /** When true, the help popover includes drag/edit shortcuts. */
  editable?: boolean
}

const TimelineToolbar = ({
  leftSlot,
  rightSlot,
  allowZoom,
  onZoomIn,
  onZoomOut,
  onResetView,
  canZoomIn = true,
  canZoomOut = true,
  canReset = true,
  allowSaveAsImage,
  onSaveAsImage,
  allowFullScreen,
  isFullScreen,
  onToggleFullScreen,
  allowSettings,
  showCurrentTime,
  onToggleCurrentTime,
  showVerticalGridlines,
  onToggleVerticalGridlines,
  showWeekends,
  onToggleWeekends,
  isCompact,
  onToggleCompact,
  onRefresh,
  editable,
}: TimelineToolbarProps) => {
  const helpRows: Array<{ keys: string; action: string }> = [
    { keys: 'Scroll', action: 'Pan vertically' },
    { keys: 'Shift + Scroll', action: 'Pan horizontally' },
    { keys: 'Click + Drag', action: 'Pan' },
    ...(allowZoom
      ? [
          { keys: 'Ctrl + Scroll', action: 'Zoom in / out' },
          { keys: '+  /  −', action: 'Zoom in / out' },
        ]
      : []),
    ...(editable
      ? [
          { keys: 'Drag bar', action: 'Move item' },
          { keys: 'Drag handle', action: 'Resize item' },
          { keys: 'Esc', action: 'Cancel drag' },
        ]
      : []),
  ]

  const helpContent = (
    <div style={{ minWidth: 240 }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Keyboard &amp; mouse controls
      </Typography.Text>
      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 8 }}>
        <tbody>
          {helpRows.map(({ keys, action }) => (
            <tr key={keys}>
              <td style={{ paddingBottom: 4, paddingRight: 16, whiteSpace: 'nowrap' }}>
                <Typography.Text keyboard style={{ fontSize: 12 }}>{keys}</Typography.Text>
              </td>
              <td style={{ paddingBottom: 4, fontSize: 12 }}>{action}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )

  // Settings menu: each item's onClick toggles the value and keeps the dropdown
  // open (by stopping the event before antd closes it). The Switch is purely
  // visual — the row click does the actual toggle so clicking the label works too.
  const settingsItems: MenuProps['items'] = [
    {
      key: 'show-current-time',
      onClick: ({ domEvent }) => {
        domEvent.stopPropagation()
        onToggleCurrentTime?.(!showCurrentTime)
      },
      label: (
        <Space>
          <Switch size="small" checked={showCurrentTime} />
          Show Current Time
        </Space>
      ),
    },
    {
      key: 'show-vertical-gridlines',
      onClick: ({ domEvent }) => {
        domEvent.stopPropagation()
        onToggleVerticalGridlines?.(!showVerticalGridlines)
      },
      label: (
        <Space>
          <Switch size="small" checked={showVerticalGridlines} />
          Show Vertical Gridlines
        </Space>
      ),
    },
    {
      key: 'show-weekends',
      onClick: ({ domEvent }) => {
        domEvent.stopPropagation()
        onToggleWeekends?.(!showWeekends)
      },
      label: (
        <Space>
          <Switch size="small" checked={showWeekends} />
          Highlight Weekends
        </Space>
      ),
    },
    {
      key: 'compact-mode',
      onClick: ({ domEvent }) => {
        domEvent.stopPropagation()
        onToggleCompact?.(!isCompact)
      },
      label: (
        <Space>
          <Switch size="small" checked={isCompact} />
          Compact Mode
        </Space>
      ),
    },
  ]

  return (
    <div className={styles.toolbar}>
      <div className={styles.toolbarLeft}>{leftSlot}</div>

      <div className={styles.toolbarRight}>
        {/* Built-in actions first; consumer-provided rightSlot sits on the far
            right (after a divider), matching the WaydGrid toolbar convention. */}
        {allowZoom && (
          <>
            <WaydTooltip title="Zoom out">
              <Button
                type="text"
                shape="circle"
                icon={<ZoomOutOutlined />}
                disabled={!canZoomOut}
                onClick={onZoomOut}
              />
            </WaydTooltip>
            <WaydTooltip title="Zoom in">
              <Button
                type="text"
                shape="circle"
                icon={<ZoomInOutlined />}
                disabled={!canZoomIn}
                onClick={onZoomIn}
              />
            </WaydTooltip>
            <WaydTooltip title="Reset view">
              <Button
                type="text"
                shape="circle"
                icon={<UndoOutlined />}
                disabled={!canReset}
                onClick={onResetView}
              />
            </WaydTooltip>
          </>
        )}
        {allowZoom &&
          (onRefresh || allowSaveAsImage || allowFullScreen || allowSettings) && (
            <span className={styles.toolbarDivider} />
          )}
        {onRefresh && (
          <WaydTooltip title="Refresh">
            <Button
              type="text"
              shape="circle"
              icon={<ReloadOutlined />}
              onClick={onRefresh}
            />
          </WaydTooltip>
        )}
        {allowSaveAsImage && (
          <WaydTooltip title="Save as Image">
            <Button
              type="text"
              shape="circle"
              icon={<FileImageOutlined />}
              onClick={onSaveAsImage}
            />
          </WaydTooltip>
        )}
        {allowFullScreen && (
          <WaydTooltip title={isFullScreen ? 'Exit Fullscreen' : 'Fullscreen'}>
            <Button
              type="text"
              shape="circle"
              icon={
                isFullScreen ? (
                  <FullscreenExitOutlined />
                ) : (
                  <FullscreenOutlined />
                )
              }
              onClick={onToggleFullScreen}
            />
          </WaydTooltip>
        )}
        <Popover
          content={helpContent}
          trigger="click"
          placement="bottomRight"
        >
          <WaydTooltip title="Help">
            <Button type="text" shape="circle" icon={<QuestionCircleOutlined />} />
          </WaydTooltip>
        </Popover>
        {allowSettings && (
          <Dropdown
            menu={{ items: settingsItems }}
            trigger={['click']}
            placement="bottomRight"
          >
            <WaydTooltip title="Settings">
              <Button type="text" shape="circle" icon={<SettingOutlined />} />
            </WaydTooltip>
          </Dropdown>
        )}
        {rightSlot &&
          (onRefresh ||
            allowSaveAsImage ||
            allowFullScreen ||
            allowSettings) && <span className={styles.toolbarDivider} />}
        {rightSlot}
      </div>
    </div>
  )
}

export default TimelineToolbar
