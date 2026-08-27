'use client'

import { CloseOutlined } from '@ant-design/icons'
import { Button, Drawer, Flex, Grid, Skeleton, Typography } from 'antd'
import { ReactNode, useEffect, useState } from 'react'
import { ConfigListConstants } from '@/src/config/theme/theme-constants'
import { useLocalStorageState } from '@/src/hooks'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import styles from './config-list-panel.module.css'

const { useBreakpoint } = Grid
const { Title } = Typography

/** Width is a display preference, shared by every config list. */
export const CONFIG_PANEL_WIDTH_KEY = 'wayd-config-list:panel-width'

/** Keyboard resize step, matching how a scrollbar arrow key behaves. */
const KEYBOARD_STEP = 16

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
  /** Actions for the open record, pinned to the panel's foot. */
  actions?: ReactNode
  /** Shows a skeleton in place of the details while the record loads. */
  isLoading?: boolean
}

const PanelContents = ({
  details,
  actions,
  isLoading,
}: Pick<ConfigListPanelProps, 'details' | 'actions' | 'isLoading'>) => (
  <>
    <div className={styles.panelBody}>
      {isLoading ? <Skeleton active paragraph={{ rows: 4 }} /> : details}
    </div>
    {actions && <div className={styles.panelActions}>{actions}</div>}
  </>
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
 * Below `md` the panel becomes a Drawer over the list, matching how the record
 * facts rail degrades and how the feature flags list already behaves.
 */
const ConfigListPanel = ({
  children,
  open,
  onClose,
  title,
  details,
  actions,
  isLoading,
}: ConfigListPanelProps) => {
  const screens = useBreakpoint()
  const compact = !screens.md
  const [dragging, setDragging] = useState(false)
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
  // a 7px target mid-drag, and a handle-bound move would drop the gesture.
  useEffect(() => {
    if (!dragging) return

    const onMove = (e: MouseEvent) => {
      // The panel is right-anchored, so it widens as the pointer moves left.
      setWidth(clamp(window.innerWidth - e.clientX))
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
          width="80%"
        >
          <Flex vertical gap={12} style={{ height: '100%' }}>
            <PanelContents
              details={details}
              actions={actions}
              isLoading={isLoading}
            />
          </Flex>
        </Drawer>
      </div>
    )
  }

  return (
    <div className={styles.layout}>
      <div className={styles.list}>{children}</div>
      {open && (
        <aside
          className={styles.panel}
          style={{ width: boundedWidth }}
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
          <div className={styles.panelHeader}>
            <Title level={5} style={{ margin: 0, fontSize: 14 }}>
              {title}
            </Title>
            <WaydTooltip title="Close">
              <Button
                type="text"
                size="small"
                aria-label="Close details panel"
                icon={<CloseOutlined />}
                onClick={onClose}
              />
            </WaydTooltip>
          </div>
          <PanelContents
            details={details}
            actions={actions}
            isLoading={isLoading}
          />
        </aside>
      )}
    </div>
  )
}

export default ConfigListPanel
