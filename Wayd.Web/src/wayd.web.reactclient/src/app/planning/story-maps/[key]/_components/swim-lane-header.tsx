'use client'

import { StoryMapSwimLaneDto } from '@/src/services/wayd-api'
import { WaydTooltip } from '@/src/components/common'
import { CalendarOutlined, DeleteOutlined } from '@ant-design/icons'
import { Button, DatePicker, Popconfirm, Popover } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC, useState } from 'react'
import { BoardActions } from './board-actions'
import InlineEditText from './inline-edit-text'
import { useBoardSortable } from './use-board-sortable'
import styles from '../../_components/story-map.module.css'

const { RangePicker } = DatePicker

const DISPLAY_FORMAT = 'D MMM YYYY'

/** "1 Mar 2026 – 14 Mar 2026", or just the one date when only a start or an end is set. */
const formatRange = (start: Date | undefined, end: Date | undefined) => {
  const from = start ? dayjs(start).format(DISPLAY_FORMAT) : null
  const to = end ? dayjs(end).format(DISPLAY_FORMAT) : null
  if (from && to) return `${from} – ${to}`
  return from ?? to
}

const toDayjs = (value: Date | undefined): Dayjs | null =>
  value ? dayjs(value).startOf('day') : null

/**
 * These are NodaTime `LocalDate`s — plain calendar dates, not instants. NSwag types them as `Date`,
 * but serializing an actual Date would emit a full UTC timestamp and can shift the day either way
 * depending on the viewer's timezone, so send the formatted `YYYY-MM-DD` the API expects. The cast
 * bridges that generated-type-vs-wire-format mismatch; the same is done for roadmap dates.
 */
const toLocalDate = (value: Dayjs | null | undefined): Date | undefined =>
  value ? (value.format('YYYY-MM-DD') as unknown as Date) : undefined

export interface SwimLaneHeaderProps {
  swimLane: StoryMapSwimLaneDto
  /** 1-based grid row of this lane's full-width header banner. */
  row: number
  /** Number of tasks in the lane, so the delete confirmation can say what will move. */
  taskCount: number
  actions: BoardActions
}

/**
 * A swim lane's full-width banner, spanning every column above its task cells. The default lane is
 * fixed — the domain forbids renaming, reordering, or removing it — so it renders as a plain label.
 */
const SwimLaneHeader: FC<SwimLaneHeaderProps> = ({
  swimLane,
  row,
  taskCount,
  actions,
}) => {
  const isFixed = swimLane.isDefault
  const [isPickingDates, setIsPickingDates] = useState(false)

  const {
    attributes,
    listeners,
    setNodeRef,
    style: sortableStyle,
  } = useBoardSortable(swimLane.id, !actions.canUpdate || isFixed)

  const dateLabel = formatRange(swimLane.startDate, swimLane.endDate)

  const handleDatesChange = (
    dates: [Dayjs | null, Dayjs | null] | null,
  ) => {
    // Clearing the whole control yields null; clearing one side yields a null in that slot.
    actions.onSetSwimLaneDates(
      swimLane.id,
      toLocalDate(dates?.[0]),
      toLocalDate(dates?.[1]),
    )
  }

  // The picker lives in a popover, so the banner shows only an icon (or the range as text).
  const datesControl = (
    <Popover
      open={isPickingDates}
      onOpenChange={setIsPickingDates}
      trigger="click"
      placement="bottomLeft"
      destroyOnHidden
      content={
        <RangePicker
          size="small"
          open
          autoFocus
          allowEmpty={[true, true]}
          placeholder={['Start', 'End']}
          format={DISPLAY_FORMAT}
          value={[toDayjs(swimLane.startDate), toDayjs(swimLane.endDate)]}
          onChange={handleDatesChange}
          getPopupContainer={(trigger) => trigger.parentElement ?? document.body}
        />
      }
    >
      <WaydTooltip title={dateLabel ? 'Change dates' : 'Set dates'}>
        <Button
          size="small"
          type="text"
          icon={<CalendarOutlined />}
          aria-label={
            dateLabel ? `Change dates (${dateLabel})` : 'Set swim lane dates'
          }
          className={`${styles.swimLaneDatesButton} ${
            // Stay visible while the popover is open, or the trigger vanishes the moment the
            // pointer leaves the banner to reach the calendar.
            dateLabel || isPickingDates ? '' : styles.swimLaneDatesEmpty
          }`}
        >
          {dateLabel}
        </Button>
      </WaydTooltip>
    </Popover>
  )

  return (
    <div
      ref={setNodeRef}
      className={styles.swimLaneHeader}
      style={{ gridRow: row, ...sortableStyle }}
      {...attributes}
      {...listeners}
      // The default lane is pinned first by the domain, so it is never draggable.
      aria-label={
        actions.canUpdate && !isFixed ? `Reorder ${swimLane.name}` : undefined
      }
    >
      <div className={styles.swimLaneHeaderSticky}>
        {isFixed ? (
          <span className={styles.swimLaneName}>{swimLane.name}</span>
        ) : (
          <InlineEditText
            value={swimLane.name}
            onSave={(name) => actions.onRenameSwimLane(swimLane.id, name)}
            disabled={!actions.canUpdate}
            autoEdit={actions.autoEditId === swimLane.id}
            ariaLabel="Rename swim lane"
            singleLine
            className={styles.swimLaneName}
            display={(v) => <span className={styles.swimLaneName}>{v}</span>}
          />
        )}

        {/* Both dates are optional and independent — a lane can have just a start, just an end,
            neither, or both. The picker's own range semantics stop an end before a start. */}
        {!isFixed &&
          (actions.canUpdate ? (
            datesControl
          ) : (
            dateLabel && (
              <span className={styles.swimLaneDatesText}>{dateLabel}</span>
            )
          ))}

        {actions.canUpdate && !isFixed && (
          <div className={styles.footerActions}>
            <Popconfirm
              title="Delete this swim lane?"
              // Removing a lane reassigns its tasks to the default lane rather than deleting them.
              description={
                taskCount > 0
                  ? `Its ${taskCount} ${taskCount === 1 ? 'task moves' : 'tasks move'} to the default lane.`
                  : undefined
              }
              okText="Delete"
              okButtonProps={{ danger: true }}
              onConfirm={() => actions.onDeleteSwimLane(swimLane.id)}
            >
              <Button
                size="small"
                type="text"
                icon={<DeleteOutlined />}
                aria-label={`Delete ${swimLane.name}`}
              />
            </Popconfirm>
          </div>
        )}
      </div>
    </div>
  )
}

export default SwimLaneHeader
