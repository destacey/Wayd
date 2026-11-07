'use client'

import { Flex, Input } from 'antd'
import { useLayoutEffect, useState } from 'react'
import dayjs from 'dayjs'
import {
  dateEditor,
  pairedEditors,
  selectEditor,
  textEditor,
  type CellEditor,
  type CellEditorProps,
} from '@/src/components/common/wayd-grid'
import { WaydColorPicker } from '@/src/components/common'
import {
  JsonPatchOperationType,
  type CreateRoadmapActivityRequest,
  type CreateRoadmapTimeboxRequest,
  type JsonPatchOperation,
  type RoadmapColorDto,
} from '@/src/services/wayd-api'
import {
  parseCalendarDate,
  toCalendarDate,
  type CalendarDate,
} from '@/src/utils/calendar-date'
import { rangeChangeContainmentError } from '@/src/utils/date-containment'
import type { RoadmapItemTreeNode } from './roadmap-items-grid'
import { datedItemsSpan } from './roadmap-parent-date-hint'
import RoadmapColorPicker from './roadmap-color-picker'

type RoadmapRow = RoadmapItemTreeNode

/** The item types a new row can be created as. */
export const CREATE_TYPE_OPTIONS = [
  { label: 'Activity', value: 'activity' },
  { label: 'Timebox', value: 'timebox' },
]

const isMilestone = (row: RoadmapRow) => row.type === 'Milestone'

const toModelDate = (value: Date | null | undefined): CalendarDate | null =>
  value ? toCalendarDate(value) : null

const toNodeDate = (value: CalendarDate | null): Date | null =>
  value ? parseCalendarDate(value).toDate() : null

/** Edits an item's name; required, 128 characters at most. */
export const roadmapNameEditor = textEditor<RoadmapRow>({
  field: 'name',
  get: (row) => row.name,
  apply: (row, name) => ({ ...row, name }),
  maxLength: 128,
  validate: (name) => (name.trim() ? undefined : 'Name is required.'),
})

/** Chooses a new row's type. An existing item keeps the type it has. */
export const roadmapTypeEditor = selectEditor<RoadmapRow, string>({
  field: 'type',
  get: (row) => row.$type,
  apply: (row, $type) => ({
    ...row,
    $type,
    type: CREATE_TYPE_OPTIONS.find((o) => o.value === $type)?.label ?? row.type,
  }),
  options: CREATE_TYPE_OPTIONS,
  editable: (_row, { isDraft }) => isDraft,
})

/**
 * An item's start and end, checked together: the end cannot come before the
 * start, and an activity's range must still contain its children unless the
 * whole range moved (a shift, which carries the children along). A
 * milestone's single date is edited in the Start column.
 */
export const [roadmapStartEditor, roadmapEndEditor] = pairedEditors<
  RoadmapRow,
  CalendarDate | null,
  CalendarDate | null
>({
  start: dateEditor<RoadmapRow>({
    field: (row) => (isMilestone(row) ? 'date' : 'start'),
    get: (row) => toModelDate(isMilestone(row) ? row.date : row.start),
    apply: (row, value) =>
      isMilestone(row)
        ? { ...row, date: toNodeDate(value) }
        : { ...row, start: toNodeDate(value) },
    validate: (value, row) =>
      value
        ? undefined
        : isMilestone(row)
          ? 'Date is required.'
          : 'Start is required.',
    requiredToCreate: true,
  }),
  end: dateEditor<RoadmapRow>({
    field: 'end',
    get: (row) => toModelDate(row.end),
    apply: (row, value) => ({ ...row, end: toNodeDate(value) }),
    validate: (value) => (value ? undefined : 'End is required.'),
    editable: (row) => !isMilestone(row),
    requiredToCreate: true,
  }),
  validate: ({ start, end }, row) => {
    if (isMilestone(row) || !start || !end) return undefined
    if (end < start) return { end: 'End must be on or after Start.' }
    const containment = rangeChangeContainmentError({
      original: [
        row.start ? dayjs(row.start) : null,
        row.end ? dayjs(row.end) : null,
      ],
      next: [parseCalendarDate(start), parseCalendarDate(end)],
      span: datedItemsSpan(row.children),
    })
    return containment ? { [containment.role]: containment.message } : undefined
  },
})

/** The hex color forms the API accepts. */
const HEX_COLOR = /^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$/

/**
 * Edits an activity's color: from the roadmap's palette when it has one,
 * else any hex color. A timebox has no color.
 */
export const roadmapColorEditor = (
  roadmapColors: RoadmapColorDto[],
): CellEditor<RoadmapRow, string | null, string | null> => ({
  field: 'color',
  get: (row) => row.color ?? null,
  apply: (row, color) => ({ ...row, color }),
  editable: (row) => row.type !== 'Timebox',
  validate: (color) =>
    !color || HEX_COLOR.test(color)
      ? undefined
      : 'Use a hex color, such as #1677FF.',
  render: (props) =>
    roadmapColors.length > 0 ? (
      <PaletteColorCellEditor {...props} roadmapColors={roadmapColors} />
    ) : (
      <FreeformColorCellEditor {...props} />
    ),
})

const isClearKey = (key: string) => key === 'Delete' || key === 'Backspace'

/** The palette list opens with the editor; a pick commits it, and Delete or
 *  Backspace clears the color. */
function PaletteColorCellEditor(
  props: CellEditorProps<string | null> & { roadmapColors: RoadmapColorDto[] },
) {
  const { onChange, commit, onPopupOpenChange } = props
  const [open, setOpen] = useState(true)
  useLayoutEffect(() => {
    onPopupOpenChange(open)
  }, [open, onPopupOpenChange])
  return (
    <div
      style={EDITOR_FILL_STYLE}
      onKeyDown={(e) => {
        if (!isClearKey(e.key)) return
        e.preventDefault()
        onChange(null)
        commit()
      }}
    >
      <RoadmapColorPicker
        entries={props.roadmapColors}
        value={props.value ?? undefined}
        open={open}
        onOpenChange={setOpen}
        size="small"
        status={props.status}
        aria-label={props['aria-label']}
        aria-invalid={props['aria-invalid']}
        aria-describedby={props['aria-describedby']}
        onChange={(next) => {
          onChange(next ?? null)
          commit()
        }}
      />
    </div>
  )
}

const EDITOR_FILL_STYLE = { display: 'flex', width: '100%' } as const

/**
 * Any color: typed as hex, or picked from the swatches, which ↓ opens and
 * Escape closes. A pick commits it; an empty value clears the color.
 */
function FreeformColorCellEditor(props: CellEditorProps<string | null>) {
  const { onChange, commit, onPopupOpenChange } = props
  const [open, setOpen] = useState(false)
  useLayoutEffect(() => {
    onPopupOpenChange(open)
  }, [open, onPopupOpenChange])
  return (
    <Flex
      align="center"
      gap={4}
      style={EDITOR_FILL_STYLE}
      onKeyDown={(e) => {
        if (open && e.key === 'Escape') {
          e.preventDefault()
          setOpen(false)
        } else if (!open && e.key === 'ArrowDown') {
          e.preventDefault()
          setOpen(true)
        }
      }}
    >
      <Input
        size="small"
        value={props.value ?? ''}
        placeholder="#RRGGBB"
        maxLength={7}
        status={props.status}
        aria-label={props['aria-label']}
        aria-invalid={props['aria-invalid']}
        aria-describedby={props['aria-describedby']}
        onChange={(e) => onChange(e.target.value.trim() || null)}
      />
      <WaydColorPicker
        value={
          props.value && HEX_COLOR.test(props.value) ? props.value : undefined
        }
        open={open}
        onOpenChange={setOpen}
        showText={false}
        onChange={(next) => {
          onChange(next ?? null)
          setOpen(false)
          commit()
        }}
      />
    </Flex>
  )
}

/** The grid's patch for an item — each field it changed — as JSON Patch. */
export const toRoadmapItemPatch = (
  patch: Record<string, unknown>,
): JsonPatchOperation[] =>
  Object.entries(patch).map(([field, value]) => ({
    op: JsonPatchOperationType.Replace,
    path: `/${field}`,
    value,
  }))

/**
 * The create request for a new row, from the values the user put on it. A
 * timebox carries no color, even one picked before the type was changed.
 */
export const toCreateRoadmapItemRequest = (
  roadmapId: string,
  parentId: string | null,
  values: Record<string, unknown>,
): CreateRoadmapActivityRequest | CreateRoadmapTimeboxRequest => {
  const $type = values.type === 'timebox' ? 'timebox' : 'activity'
  const color =
    $type === 'activity' && typeof values.color === 'string'
      ? values.color
      : undefined
  return {
    $type,
    roadmapId,
    parentId: parentId ?? undefined,
    name: String(values.name ?? ''),
    start: values.start as string,
    end: values.end as string,
    ...(color ? { color } : {}),
  }
}
