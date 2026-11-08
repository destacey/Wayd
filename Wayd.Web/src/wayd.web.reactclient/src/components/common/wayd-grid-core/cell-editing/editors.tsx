'use client'

import { DatePicker, Input, InputNumber, Select } from 'antd'
import { useLayoutEffect, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import customParseFormat from 'dayjs/plugin/customParseFormat'
import {
  parseCalendarDate,
  toCalendarDate,
  type CalendarDate,
} from '@/src/utils/calendar-date'
import type { CellEditor, CellEditorProps, EditorGroup } from './types'

dayjs.extend(customParseFormat)

/** What every built-in editor factory takes besides its own options. */
type EditorBase<T, V> = Pick<
  CellEditor<T, V>,
  | 'field'
  | 'get'
  | 'apply'
  | 'validate'
  | 'editable'
  | 'equals'
  | 'requiredToCreate'
>

/** The ARIA a control takes from the grid: its name and its error state. */
const ariaOf = <E,>(props: CellEditorProps<E>) => ({
  'aria-label': props['aria-label'],
  'aria-invalid': props['aria-invalid'],
  'aria-describedby': props['aria-describedby'],
})

/** A single-line text editor; typing a character starts it from that character. */
export function textEditor<T>(
  options: EditorBase<T, string> & { maxLength?: number },
): CellEditor<T, string, string> {
  return {
    ...options,
    hintMode: 'text',
    format: (value) => value ?? '',
    parse: (value) => value,
    fromInput: (text) => text,
    render: (props) => (
      <Input
        size="small"
        value={props.value}
        maxLength={options.maxLength}
        status={props.status}
        {...ariaOf(props)}
        onChange={(e) => props.onChange(e.target.value)}
      />
    ),
  }
}

/** A number editor; its value is `null` when empty. Typing a digit starts it
 *  from that digit; any other key opens it empty. */
export function numberEditor<T>(
  options: EditorBase<T, number | null> & {
    min?: number
    max?: number
    suffix?: string
  },
): CellEditor<T, number | null, number | null> {
  return {
    ...options,
    hintMode: 'text',
    fromInput: (text) => (/^\d$/.test(text) ? Number(text) : null),
    render: (props) => (
      <InputNumber<number>
        size="small"
        value={props.value}
        min={options.min}
        max={options.max}
        suffix={options.suffix}
        status={props.status}
        {...ariaOf(props)}
        style={{ width: '100%' }}
        onChange={(next) => props.onChange(next ?? null)}
      />
    ),
  }
}

/** One choice a {@link selectEditor} offers. */
export interface SelectEditorOption<V> {
  /** What the list shows, and what typing searches. */
  label: string
  /** What the cell's field is set to. */
  value: V
}

/** An option's value: an element of the list a multiple choice holds. */
type OptionValue<V> = V extends readonly (infer E)[] ? E : V

/**
 * A choice from a list. The list opens with the editor; picking a single
 * value commits it. With `multiple`, the value is a list of the options'
 * values and the editor stays open until the user leaves the cell.
 */
export function selectEditor<T, V>(
  options: EditorBase<T, V> & {
    options:
      | SelectEditorOption<OptionValue<V>>[]
      | ((row: T) => SelectEditorOption<OptionValue<V>>[])
    multiple?: boolean
  },
): CellEditor<T, V, V> {
  const { options: choices, multiple, ...base } = options
  return {
    ...base,
    hintMode: multiple ? 'multiselect' : 'select',
    render: (props) => (
      <SelectCellEditor
        {...props}
        multiple={multiple}
        choices={
          choices as
            | SelectEditorOption<unknown>[]
            | ((row: unknown) => SelectEditorOption<unknown>[])
        }
      />
    ),
  }
}

function SelectCellEditor<V>(
  props: CellEditorProps<V> & {
    choices:
      | SelectEditorOption<unknown>[]
      | ((row: unknown) => SelectEditorOption<unknown>[])
    multiple?: boolean
  },
) {
  const { value, onChange, commit, onPopupOpenChange, status, row } = props
  const { choices, multiple } = props
  const list = typeof choices === 'function' ? choices(row) : choices
  // The list opens with the editor; the grid is told of every change, the
  // first included, so it knows Escape belongs to the list.
  const [open, setOpen] = useState(true)
  useLayoutEffect(() => {
    onPopupOpenChange(open)
  }, [open, onPopupOpenChange])
  return (
    <Select
      size="small"
      value={value as never}
      options={list as never}
      mode={multiple ? 'multiple' : undefined}
      // One line of tags, the rest counted, so the row never grows.
      maxTagCount={multiple ? 'responsive' : undefined}
      open={open}
      showSearch={{ optionFilterProp: 'label' }}
      status={status}
      {...ariaOf(props)}
      style={{ width: '100%' }}
      onOpenChange={setOpen}
      onChange={(next) => {
        onChange(next as V)
        if (!multiple) commit()
      }}
    />
  )
}

/** The input format of a date cell, and the one typed dates are read in. */
const DATE_INPUT_FORMAT = 'YYYY-MM-DD'

/**
 * A calendar date (`YYYY-MM-DD`), or `null` when cleared. Pair a start and an
 * end with {@link pairedEditors} so they are checked and sent together.
 *
 * The calendar stays closed until clicked or opened with ↓, so a typed date
 * commits on the first Enter or Tab like any other cell; picking a day from
 * the calendar commits it.
 */
export function dateEditor<T>(
  options: EditorBase<T, CalendarDate | null>,
): CellEditor<T, CalendarDate | null, Dayjs | null> {
  return {
    ...options,
    hintMode: 'date',
    format: (value) => (value ? parseCalendarDate(value) : null),
    parse: (value) => (value ? toCalendarDate(value) : null),
    render: (props) => <DateCellEditor {...props} />,
  }
}

function DateCellEditor(props: CellEditorProps<Dayjs | null>) {
  const { value, onChange, commit, onPopupOpenChange, status } = props
  const [open, setOpen] = useState(false)
  useLayoutEffect(() => {
    onPopupOpenChange(open)
  }, [open, onPopupOpenChange])
  return (
    <div
      onMouseDown={() => setOpen(true)}
      onKeyDown={(e) => {
        if (e.key === 'ArrowDown' && !open) {
          e.preventDefault()
          setOpen(true)
        }
      }}
      // The picker only takes typed text on Enter or blur; read it as it is
      // typed, so a commit by Tab or a click elsewhere keeps it.
      onInput={(e) => {
        const text = (e.target as HTMLInputElement).value.trim()
        if (!text) {
          onChange(null)
          return
        }
        const typed = dayjs(text, DATE_INPUT_FORMAT, true)
        if (typed.isValid()) onChange(typed)
      }}
    >
      <DatePicker
        size="small"
        value={value}
        format={DATE_INPUT_FORMAT}
        open={open}
        status={status}
        {...ariaOf(props)}
        style={{ width: '100%' }}
        onOpenChange={(next) => {
          // The picker asks to open on focus; only a click or ↓ opens it.
          if (!next) setOpen(false)
        }}
        onChange={(next) => {
          onChange(next ?? null)
          if (open) {
            setOpen(false)
            commit()
          }
        }}
      />
    </div>
  )
}

/**
 * Links fields that must be valid together — a start and an end — into one
 * group. Moving between the group's cells keeps their values unsaved; when
 * focus leaves the group the values are checked together by `validate` (keyed
 * by role) and the changed ones are sent in a single patch.
 *
 * A row where only one member is editable (a milestone's single date)
 * commits that member alone, still through `validate`.
 */
export function pairedEditors<T, S, E>(pair: {
  start: CellEditor<T, S, any>
  end: CellEditor<T, E, any>
  validate?: (
    values: { start: S; end: E },
    row: T,
  ) => Partial<Record<'start' | 'end', string>> | undefined
}): [CellEditor<T, S, any>, CellEditor<T, E, any>] {
  const group: EditorGroup = {
    id: {},
    validate: pair.validate as EditorGroup['validate'],
  }
  return [
    { ...pair.start, group, role: 'start' },
    { ...pair.end, group, role: 'end' },
  ]
}
