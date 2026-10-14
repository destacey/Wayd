'use client'

import { useState } from 'react'
import Link from 'next/link'
import { Button, InputNumber, Table, Tooltip, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { MinusSquareOutlined, PlusSquareOutlined } from '@ant-design/icons'
import { CalendarDate, formatCalendarDate } from '@/src/utils'
import {
  formatPersonDays,
  NO_ROLE_LABEL,
} from '../team-capacity/team-capacity-data'
import {
  dayEditKey,
  pendingPercent,
  PlanCell,
  planCell,
  PlanRow,
  PlanWeek,
  percentLabel,
  RowEdits,
  weekEditKey,
  weekIsEdited,
  withCellEdit,
} from './capacity-plan-data'
import styles from './capacity-plan-grid.module.css'

const { Text } = Typography

export interface CapacityPlanGridProps {
  rows: PlanRow[]
  weeks: PlanWeek[]
  /** Whether the cells are inputs. */
  editing: boolean
  /** The percents entered so far, by row id and then edit key. */
  edits: Record<string, RowEdits>
  /** Replaces a row's edits. */
  onEditRow: (rowId: string, edits: RowEdits) => void
  isLoading?: boolean
}

/** What a cell's tooltip says beside its percent. */
export function cellDetails(cell: PlanCell, dayCount: number): string[] {
  return [
    `${formatPersonDays(cell.personDays)} person-days over ${dayCount} working ${dayCount === 1 ? 'day' : 'days'}`,
    ...(cell.holidays > 0
      ? [`${cell.holidays} ${cell.holidays === 1 ? 'holiday' : 'holidays'}`]
      : []),
    ...(cell.awayDays > 0
      ? [
          `Away for some of ${cell.awayDays} ${cell.awayDays === 1 ? 'day' : 'days'}`,
        ]
      : []),
    ...(cell.maxElsewherePercent > 0
      ? [`Up to ${cell.maxElsewherePercent}% allocated to other teams`]
      : []),
  ]
}

/**
 * A team's people against the weeks of a plan, each week expandable to its
 * working days. A cell shows the percent allocated and, on hover, what it is
 * worth after holidays and time away, which are marked but never edited here.
 * In editing, a cell takes the percent for its week or day.
 */
const CapacityPlanGrid = ({
  rows,
  weeks,
  editing,
  edits,
  onEditRow,
  isLoading,
}: CapacityPlanGridProps) => {
  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  const toggle = (week: PlanWeek) =>
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(week.index)) next.delete(week.index)
      else next.add(week.index)
      return next
    })

  const allDays = weeks.flatMap((week) => week.days)

  const cellView = (
    row: PlanRow,
    week: PlanWeek,
    day: CalendarDate | undefined,
    label: string,
  ) => {
    const days = day ? [day] : week.days
    const cell = planCell(row, days)
    const rowEdits = edits[row.id]
    // What the cell will hold once saved, so expanding or collapsing a week
    // never hides an edit that Save would still send.
    const pending = days.map((d) => pendingPercent(row, week, d, rowEdits))
    const pendingCell = {
      ...cell,
      minPercent: pending.length ? Math.min(...pending) : 0,
      maxPercent: pending.length ? Math.max(...pending) : 0,
    }
    const edited = day
      ? rowEdits?.[dayEditKey(day)] !== undefined ||
        rowEdits?.[weekEditKey(week)] !== undefined
      : weekIsEdited(week, rowEdits)
    const marked = cell.holidays > 0 || cell.awayDays > 0
    const className = [
      styles.cell,
      cell.holidays > 0 && cell.holidays === days.length ? styles.holiday : '',
      cell.awayDays > 0 ? styles.away : '',
      edited ? styles.edited : '',
    ]
      .filter(Boolean)
      .join(' ')

    // A week of days the team does not work has nothing to show or set.
    if (days.length === 0) {
      return (
        <div
          className={className}
          data-testid={`cell-${row.id}-${weekEditKey(week)}`}
        >
          <Text type="secondary">—</Text>
        </div>
      )
    }

    const details = (
      <>
        {cellDetails(cell, days.length).map((line) => (
          <div key={line}>{line}</div>
        ))}
      </>
    )

    if (editing) {
      return (
        <Tooltip title={details}>
          <div className={className}>
            <InputNumber
              size="small"
              min={0}
              max={100}
              precision={0}
              suffix="%"
              className={styles.input}
              aria-label={`${label} for ${row.employee.name}, ${row.role?.name ?? NO_ROLE_LABEL}`}
              placeholder={percentLabel(pendingCell)}
              value={
                pendingCell.minPercent === pendingCell.maxPercent
                  ? pendingCell.minPercent
                  : null
              }
              onChange={(value) =>
                onEditRow(
                  row.id,
                  withCellEdit(rowEdits ?? {}, week, day, value ?? undefined),
                )
              }
            />
          </div>
        </Tooltip>
      )
    }

    return (
      <Tooltip title={details}>
        <div
          className={className}
          data-testid={`cell-${row.id}-${day ? dayEditKey(day) : weekEditKey(week)}`}
        >
          <Text>{percentLabel(pendingCell)}</Text>
          {marked && <span className={styles.marker} aria-hidden />}
        </div>
      </Tooltip>
    )
  }

  const weekColumns: ColumnsType<PlanRow> = weeks.map((week) => {
    const isExpanded = expanded.has(week.index)
    const label =
      week.start === week.end
        ? formatCalendarDate(week.start, 'MMM D')
        : `${formatCalendarDate(week.start, 'MMM D')} – ${formatCalendarDate(week.end, 'MMM D')}`
    const title = (
      <span className={styles.weekTitle}>
        <Button
          type="text"
          size="small"
          icon={isExpanded ? <MinusSquareOutlined /> : <PlusSquareOutlined />}
          aria-label={`${isExpanded ? 'Collapse' : 'Expand'} week of ${label}`}
          aria-expanded={isExpanded}
          disabled={week.days.length === 0}
          onClick={() => toggle(week)}
        />
        {`Week ${week.index + 1}`}
        <Text type="secondary" className={styles.weekDates}>
          {label}
        </Text>
      </span>
    )

    if (isExpanded) {
      return {
        key: weekEditKey(week),
        title,
        children: week.days.map((day) => ({
          key: dayEditKey(day),
          title: formatCalendarDate(day, 'ddd D'),
          width: 88,
          align: 'center' as const,
          render: (_: unknown, row: PlanRow) =>
            cellView(row, week, day, formatCalendarDate(day)),
        })),
      }
    }

    return {
      key: weekEditKey(week),
      title,
      width: 140,
      align: 'center' as const,
      render: (_: unknown, row: PlanRow) =>
        cellView(row, week, undefined, `Week of ${label}`),
    }
  })

  const columns: ColumnsType<PlanRow> = [
    {
      key: 'person',
      title: 'Person',
      fixed: 'left',
      width: 180,
      render: (_, row) => (
        <Link href={`/organizations/employees/${row.employee.key}`}>
          {row.employee.name}
        </Link>
      ),
    },
    {
      key: 'role',
      title: 'Role',
      fixed: 'left',
      width: 130,
      render: (_, row) =>
        row.role ? (
          <span>
            {row.role.name}
            {!row.countsTowardDeliveryCapacity && (
              <Text type="secondary"> (not delivery)</Text>
            )}
          </span>
        ) : (
          <Text type="secondary">{NO_ROLE_LABEL}</Text>
        ),
    },
    ...weekColumns,
    {
      key: 'personDays',
      title: 'Person-days',
      fixed: 'right',
      width: 110,
      align: 'right',
      render: (_, row) => formatPersonDays(planCell(row, allDays).personDays),
    },
  ]

  return (
    <Table<PlanRow>
      size="small"
      bordered
      rowKey="id"
      columns={columns}
      dataSource={rows}
      loading={isLoading}
      pagination={false}
      scroll={{ x: 'max-content' }}
      locale={{ emptyText: 'No one is allocated for these dates.' }}
    />
  )
}

export default CapacityPlanGrid
