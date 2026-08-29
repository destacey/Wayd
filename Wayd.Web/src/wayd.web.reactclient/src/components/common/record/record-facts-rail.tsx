'use client'

import { LeftOutlined } from '@ant-design/icons'
import { Button, Flex, Typography } from 'antd'
import { ReactNode, useEffect, useRef, useState } from 'react'
import { RecordLayoutConstants } from '@/src/config/theme/theme-constants'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import styles from './record-layout.module.css'

const { Title } = Typography

const PANEL_TITLE = 'Details'

/** Keyboard resize step, matching how a scrollbar arrow key behaves. */
const KEYBOARD_STEP = 16

export interface RecordFactsRailProps {
  /** The facts themselves — a stack of `LabeledContent`. */
  children: ReactNode
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Current panel width. Owned by the layout so it can be persisted. */
  width: number
  onWidthChange: (width: number) => void
}

/**
 * The record's stable facts, in a panel beside the section content.
 *
 * It shares the row with the content rather than floating over it, so the
 * content reflows to fit and nothing is hidden behind the panel. Closed by
 * default: the facts are reference material, so the content keeps the full
 * width until they are asked for.
 */
const RecordFactsRail = ({
  children,
  open,
  onOpenChange,
  width,
  onWidthChange,
}: RecordFactsRailProps) => {
  const [dragging, setDragging] = useState(false)

  // The width being dragged, held locally until the gesture ends.
  //
  // `onWidthChange` persists to localStorage and re-renders the whole record,
  // which is far too much to do per mousemove — it made the panel and every
  // chart in the content flicker for seconds after a drag. Committing once on
  // mouseup keeps the drag itself a cheap local update.
  const [draftWidth, setDraftWidth] = useState<number | null>(null)
  const liveWidth = draftWidth ?? width

  // Seeds a drag without making `width` a dependency of the listener effect,
  // which would tear down and re-bind the handlers mid-gesture.
  const widthRef = useRef(width)
  useEffect(() => {
    widthRef.current = width
  }, [width])

  const clamp = (w: number) =>
    Math.min(
      RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH,
      Math.max(RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH, w),
    )

  // Listeners go on the document, not the handle: the pointer routinely leaves
  // a 6px target mid-drag, and a handle-bound move would drop the gesture.
  useEffect(() => {
    if (!dragging) return

    let latest = clamp(widthRef.current)

    const onMove = (e: MouseEvent) => {
      // The panel is right-anchored, so it widens as the pointer moves left.
      latest = clamp(window.innerWidth - e.clientX)
      setDraftWidth(latest)
    }
    const onUp = () => {
      setDragging(false)
      setDraftWidth(null)
      onWidthChange(latest)
    }

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
  }, [dragging, onWidthChange])

  const onHandleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return
    e.preventDefault()
    // Left widens: the panel is anchored right, so it grows leftward.
    const delta = e.key === 'ArrowLeft' ? KEYBOARD_STEP : -KEYBOARD_STEP
    onWidthChange(clamp(width + delta))
  }

  if (!open) {
    return (
      <div className={styles.factsHandle}>
        <WaydTooltip title={`Show ${PANEL_TITLE}`} placement="left">
          <Button
            type="text"
            size="small"
            aria-label={`Show ${PANEL_TITLE} panel`}
            aria-expanded={false}
            icon={<LeftOutlined />}
            onClick={() => onOpenChange(true)}
          />
        </WaydTooltip>
      </div>
    )
  }

  return (
    <aside
      className={styles.factsRail}
      style={{ width: liveWidth }}
      aria-label={PANEL_TITLE}
    >
      <div
        className={`${styles.factsResizer} ${dragging ? styles.factsResizerActive : ''}`}
        role="separator"
        aria-orientation="vertical"
        aria-label={`Resize ${PANEL_TITLE} panel`}
        aria-valuenow={liveWidth}
        aria-valuemin={RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH}
        aria-valuemax={RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH}
        tabIndex={0}
        onMouseDown={(e) => {
          e.preventDefault()
          setDragging(true)
        }}
        onKeyDown={onHandleKeyDown}
      />
      <Flex align="center" justify="space-between" gap="small">
        <Title level={5} style={{ margin: 0, fontSize: 14 }}>
          {PANEL_TITLE}
        </Title>
        <WaydTooltip title={`Hide ${PANEL_TITLE}`}>
          <Button
            type="text"
            size="small"
            aria-label={`Hide ${PANEL_TITLE} panel`}
            aria-expanded
            icon={<LeftOutlined rotate={180} />}
            onClick={() => onOpenChange(false)}
          />
        </WaydTooltip>
      </Flex>
      <Flex vertical gap={10} className={styles.factsBody}>
        {children}
      </Flex>
    </aside>
  )
}

export default RecordFactsRail
