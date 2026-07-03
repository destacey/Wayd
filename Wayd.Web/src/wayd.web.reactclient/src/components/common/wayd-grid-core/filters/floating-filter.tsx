'use client'

import { useEffect, useRef, useState } from 'react'
import { DatePicker, InputNumber, Input } from 'antd'
import dayjs from 'dayjs'

import styles from './floating-filter.module.css'
import {
  createEmptyFilterModel,
  defaultOperatorFor,
  operatorNeedsValue,
  type ColumnFilterModel,
  type ConditionModel,
  type DateCondition,
  type DateTimeCondition,
  type FilterType,
  type NumberCondition,
  type TextCondition,
} from './filter-model'

const TEXT_DEBOUNCE_MS = 250

export interface FloatingFilterProps {
  /** Condition-based filter type (set columns use SetFilterPanel, not this). */
  filterType: Exclude<FilterType, 'set'>
  /** Current descriptor for the column, or undefined when unfiltered. */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  placeholder?: string
}

/**
 * Compact single-condition filter shown inline under a column header — the
 * counterpart to AG Grid's floating filter, for text/number/date columns. It
 * reads and writes the *first* condition of the column's {@link ColumnFilterModel}
 * descriptor, so it and the full {@link FilterPopup} are two UIs over one model:
 * editing here updates `conditions[0]` while preserving its operator and any
 * further conditions the popup added. (Set columns use {@link SetFilterPanel}.)
 */
const FloatingFilter = ({
  filterType,
  value,
  onChange,
  placeholder,
}: FloatingFilterProps) => {
  return (
    <ConditionFloatingFilter
      filterType={filterType}
      value={value as ConditionModel | undefined}
      onChange={onChange}
      placeholder={placeholder}
    />
  )
}

interface ConditionFloatingFilterProps {
  filterType: ConditionModel['type']
  value: ConditionModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  placeholder?: string
}

/**
 * Writes the first condition of a condition-based descriptor. The operator is
 * taken from the existing descriptor when present, otherwise the type default,
 * so the floating input never clobbers an operator chosen in the popup.
 */
const ConditionFloatingFilter = ({
  filterType,
  value,
  onChange,
  placeholder,
}: ConditionFloatingFilterProps) => {
  // Guard: only a matching condition descriptor has `conditions`. Anything else
  // (e.g. a `set` descriptor on a combined column) is treated as unfiltered here.
  const conditionValue = value?.type === filterType ? value : undefined
  const first = conditionValue?.conditions[0]
  const op = first?.op ?? defaultOperatorFor(filterType)

  /**
   * Emit a new descriptor with `conditions[0]` patched. Passing an empty
   * primary value clears the whole descriptor (unless a valueless operator like
   * blank/notBlank is active, or the popup added extra conditions).
   */
  const emitPrimary = (primary: string | number | null) => {
    const cleared =
      primary === null || primary === '' || primary === undefined
    const extraConditions = conditionValue
      ? conditionValue.conditions.length > 1
      : false
    const valueless = !operatorNeedsValue(op)

    if (cleared && !valueless && !extraConditions) {
      onChange(undefined)
      return
    }

    const base = (conditionValue ??
      createEmptyFilterModel(filterType)) as ConditionModel
    const nextConditions = [...base.conditions]
    nextConditions[0] = { ...nextConditions[0], op, value: primary } as
      | TextCondition
      | NumberCondition
      | DateCondition
      | DateTimeCondition
    onChange({ ...base, conditions: nextConditions } as ColumnFilterModel)
  }

  if (filterType === 'text') {
    return (
      <DebouncedTextInput
        value={(first as TextCondition | undefined)?.value ?? ''}
        placeholder={placeholder}
        onCommit={(v) => emitPrimary(v)}
      />
    )
  }

  if (filterType === 'number') {
    const current = (first as NumberCondition | undefined)?.value ?? null
    return (
      <InputNumber
        size="small"
        className={styles.control}
        placeholder={placeholder}
        value={current}
        onChange={(v) => emitPrimary(v ?? null)}
      />
    )
  }

  // date / dateTime
  const showTime = filterType === 'dateTime'
  const current = (first as DateCondition | DateTimeCondition | undefined)?.value
  const toIso = (d: dayjs.Dayjs | null) =>
    d ? (showTime ? d.toISOString() : d.format('YYYY-MM-DD')) : null

  return (
    <DatePicker
      size="small"
      showTime={showTime}
      className={styles.control}
      placeholder={placeholder}
      value={current ? dayjs(current) : null}
      onChange={(d) => emitPrimary(toIso(d))}
    />
  )
}

interface DebouncedTextInputProps {
  /**
   * External committed value. Also used as the remount key, so a change that
   * did NOT originate from this input's own typing (Clear, a popup edit) resets
   * the field. The input is otherwise uncontrolled — the DOM owns keystrokes.
   */
  value: string
  placeholder?: string
  onCommit: (value: string) => void
}

/**
 * Debounced text input for the floating filter.
 *
 * The input is *uncontrolled* (`defaultValue`, not `value`): the DOM owns the
 * text so every keystroke lands regardless of re-renders, and the debounced
 * descriptor echo can't clobber characters mid-typing — the bug a controlled
 * value caused. External resets (Clear / popup edits) are applied by remounting
 * via `key={value}`, which reseeds `defaultValue`. A ref tracks what we last
 * committed so an external change can be distinguished from our own echo.
 */
const DebouncedTextInput = ({
  value,
  placeholder,
  onCommit,
}: DebouncedTextInputProps) => {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  // Tracks the value this input itself last committed. When `value` matches it,
  // the incoming prop is our own debounced echo → don't remount (keeps focus &
  // in-flight keystrokes). When it differs, the change came from elsewhere
  // (Clear / popup edit) → remount to reseed defaultValue.
  const [lastCommitted, setLastCommitted] = useState(value)

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current)
    }
  }, [])

  const commit = (next: string) => {
    setLastCommitted(next)
    onCommit(next)
  }

  const handleChange = (next: string) => {
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => commit(next), TEXT_DEBOUNCE_MS)
  }

  const remountKey = value === lastCommitted ? 'typing' : value

  return (
    <Input
      key={remountKey}
      size="small"
      allowClear
      className={styles.control}
      placeholder={placeholder}
      defaultValue={value}
      onBlur={(e) => {
        // Flush any pending debounce so a quick type-then-blur isn't lost.
        if (timerRef.current) {
          clearTimeout(timerRef.current)
          timerRef.current = null
          commit(e.target.value)
        }
      }}
      onChange={(e) => handleChange(e.target.value)}
    />
  )
}

export default FloatingFilter
