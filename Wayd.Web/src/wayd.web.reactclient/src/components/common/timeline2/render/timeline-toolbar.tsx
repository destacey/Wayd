'use client'

// timeline2/render/timeline-toolbar.tsx — the timeline's action bar, following
// the tree-grid toolbar pattern (WaydTooltip + text/circle icon buttons +
// leftSlot/rightSlot + dividers). leftSlot is where drill-through +/- controls
// will live; the built-in right-side actions are save-as-image and fullscreen.

import { ReactNode } from 'react'
import { Button, Dropdown, Space, Switch, type MenuProps } from 'antd'
import {
  FileImageOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
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
  /** Refresh action — shown (ReloadOutlined) only when provided, like WaydGrid. */
  onRefresh?: () => void
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
  onRefresh,
}: TimelineToolbarProps) => {
  // Settings menu: keep the dropdown open while toggling the switch (stopPropagation
  // on the row), matching the legacy timeline's control menu behaviour.
  const settingsItems: MenuProps['items'] = [
    {
      key: 'show-current-time',
      label: (
        <Space onClick={(e) => e.stopPropagation()}>
          <Switch
            size="small"
            checked={showCurrentTime}
            onChange={(checked) => onToggleCurrentTime?.(checked)}
          />
          Show Current Time
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
