'use client'

import { CloseOutlined, MoreOutlined } from '@ant-design/icons'
import {
  Button,
  Drawer,
  Dropdown,
  Flex,
  Grid,
  MenuProps,
  Skeleton,
  Typography,
} from 'antd'
import { ReactNode, useEffect, useState } from 'react'
import { ConfigListConstants } from '@/src/config/theme/theme-constants'
import { useLocalStorageState, useRemainingHeight } from '@/src/hooks'
import { getDrawerWidthPixels } from '@/src/utils'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import styles from './config-list-panel.module.css'

const { useBreakpoint } = Grid
const { Title } = Typography

/** Width is a display preference, shared by every config list. */
export const CONFIG_PANEL_WIDTH_KEY = 'wayd-config-list:panel-width'

/** Keyboard resize step, matching how a scrollbar arrow key behaves. */
const KEYBOARD_STEP = 16

/**
 * Half the gap the resize handle fills, so a drag measures from the handle's
 * centre rather than its left edge. Matches `--config-list-gap` in the
 * stylesheet, kept in step by hand — the drag reads pointer position in page
 * coordinates, which CSS cannot tell it.
 */
const RESIZER_OFFSET = 8

export interface ConfigListPanelProps {
  /** The list itself — a `WaydGrid` wired to `onRowActivate`. */
  children: ReactNode
  /** Whether a record is open. */
  open: boolean
  /** Closes the panel. */
  onClose: () => void
  /** The open record's name, shown as the panel's title. */
  title?: string
  /** The record's fields — a stack of `LabeledContent`. */
  details: ReactNode
  /**
   * Menu items for the open record, shown behind a `⋯` in the panel header
   * beside its name — where the record's identity is, and where the feature
   * flags drawer already puts them.
   */
  actionItems?: MenuProps['items']
  /** Shows a skeleton in place of the details while the record loads. */
  isLoading?: boolean
}

const PanelContents = ({
  details,
  isLoading,
}: Pick<ConfigListPanelProps, 'details' | 'isLoading'>) => (
  <div className={styles.panelBody}>
    {isLoading ? <Skeleton active paragraph={{ rows: 4 }} /> : details}
  </div>
)

/**
 * A settings list with its record's fields in a panel beside it.
 *
 * The counterpart to `RecordLayout` for config records that have nothing to
 * say beyond their own fields — an expenditure category, an estimation scale.
 * Giving those a record page spends a navigation round trip on one
 * `Descriptions` block, when the real task is scanning and editing sibling
 * rows. Here the list stays on screen and the panel changes under it.
 *
 * A record with content that is not a flat list of its fields — criteria,
 * stages, a permission matrix — is not this. That is a record page; see
 * `docs/contributing/record-pages.mdx`.
 *
 * The panel is a plain region sharing the row with the list, **not** an antd
 * `Drawer`: it is non-modal, so the grid stays live beside it and clicking
 * down a list swaps the panel's contents with no dismiss step in between.
 * That is the whole reason this shape beats a drawer for comparing sibling
 * records. Only below `md`, where there is no room for two columns, does it
 * fall back to a real `Drawer` over the list — matching how the record facts
 * rail degrades and how the feature flags list already behaves.
 */
const ConfigListPanel = ({
  children,
  open,
  onClose,
  title,
  details,
  actionItems,
  isLoading,
}: ConfigListPanelProps) => {
  const screens = useBreakpoint()
  const compact = !screens.md
  const [dragging, setDragging] = useState(false)
  // The grid inside sizes itself with this same hook, filling to the bottom of
  // the viewport. Measuring the row here gives the panel the identical height,
  // so the two line up top and bottom instead of the panel running past the
  // grid it sits beside.
  const [rowRef, rowHeight] = useRemainingHeight()
  // Lazy, because it reads window.innerWidth — matching how the other drawers
  // in settings size themselves.
  const [drawerSize, setDrawerSize] = useState(() =>
    typeof window === 'undefined' ? undefined : getDrawerWidthPixels(),
  )
  const [width, setWidth] = useLocalStorageState<number>(
    CONFIG_PANEL_WIDTH_KEY,
    ConfigListConstants.PANEL_WIDTH,
    { version: 1 },
  )

  const clamp = (w: number) =>
    Math.min(
      ConfigListConstants.PANEL_MAX_WIDTH,
      Math.max(ConfigListConstants.PANEL_MIN_WIDTH, w),
    )

  // A stored width from a wider window (or a hand-edited value) must not be
  // able to squeeze the list out, so the bounds are enforced on read too.
  const boundedWidth = clamp(width)

  // Listeners go on the document, not the handle: the pointer routinely leaves
  // the handle mid-drag, and a handle-bound move would drop the gesture.
  useEffect(() => {
    if (!dragging) return

    const onMove = (e: MouseEvent) => {
      // The panel is right-anchored, so it widens as the pointer moves left.
      // The handle sits in the gap beside the panel, so its offset comes off
      // the width or the grip drifts away from the cursor mid-drag.
      setWidth(clamp(window.innerWidth - e.clientX - RESIZER_OFFSET))
    }
    const onUp = () => setDragging(false)

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
    // Without this the drag selects the text it passes over.
    const previousUserSelect = document.body.style.userSelect
    document.body.style.userSelect = 'none'

    return () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      document.body.style.userSelect = previousUserSelect
    }
  }, [dragging, setWidth])

  const onHandleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return
    e.preventDefault()
    // Left widens: the panel is anchored right, so it grows leftward.
    const delta = e.key === 'ArrowLeft' ? KEYBOARD_STEP : -KEYBOARD_STEP
    setWidth(clamp(boundedWidth + delta))
  }

  // Nothing rather than an empty dropdown, so a read-only viewer gets no
  // affordance that opens onto an empty menu.
  const actionsMenu = actionItems?.length ? (
    <Dropdown
      menu={{ items: actionItems }}
      trigger={['click']}
      // The ⋯ sits at the panel's right edge, so a left-aligned menu would
      // hang out over the content beside it. Aligning the menu's right edge
      // under the trigger keeps it inside the panel.
      placement="bottomRight"
    >
      <Button
        type="text"
        size="small"
        aria-label="Record actions"
        icon={<MoreOutlined />}
      />
    </Dropdown>
  ) : null

  if (compact) {
    return (
      <div className={styles.layout}>
        <div className={styles.list}>{children}</div>
        <Drawer
          title={title}
          placement="right"
          open={open}
          onClose={onClose}
          destroyOnHidden
          size={drawerSize}
          resizable={{ onResize: setDrawerSize }}
          extra={actionsMenu}
        >
          <PanelContents details={details} isLoading={isLoading} />
        </Drawer>
      </div>
    )
  }

  return (
    <div className={styles.layout} ref={rowRef}>
      <div className={styles.list}>{children}</div>
      {open && (
        <aside
          className={styles.panel}
          style={{ width: boundedWidth, height: rowHeight }}
          aria-label={title ? `${title} details` : 'Details'}
        >
          <div
            className={`${styles.resizer} ${dragging ? styles.resizerActive : ''}`}
            role="separator"
            aria-orientation="vertical"
            aria-label="Resize details panel"
            aria-valuenow={boundedWidth}
            aria-valuemin={ConfigListConstants.PANEL_MIN_WIDTH}
            aria-valuemax={ConfigListConstants.PANEL_MAX_WIDTH}
            tabIndex={0}
            onMouseDown={(e) => {
              e.preventDefault()
              setDragging(true)
            }}
            onKeyDown={onHandleKeyDown}
          />
          <div className={styles.panelScroll}>
            <div className={styles.panelHeader}>
              <Title level={5} style={{ margin: 0, fontSize: 14 }}>
                {title}
              </Title>
              <Flex align="center" gap={4}>
                {actionsMenu}
                <WaydTooltip title="Close">
                  <Button
                    type="text"
                    size="small"
                    aria-label="Close details panel"
                    icon={<CloseOutlined />}
                    onClick={onClose}
                  />
                </WaydTooltip>
              </Flex>
            </div>
            <PanelContents details={details} isLoading={isLoading} />
          </div>
        </aside>
      )}
    </div>
  )
}

export default ConfigListPanel
