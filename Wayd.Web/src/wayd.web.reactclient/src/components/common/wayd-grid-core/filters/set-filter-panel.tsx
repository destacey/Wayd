'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { Button, Checkbox, Input, type InputRef } from 'antd'
import { SearchOutlined } from '@ant-design/icons'

import styles from './set-filter-panel.module.css'
import type { FilterOption } from '../types'
import {
  SET_FILTER_BLANK,
  SET_FILTER_BLANK_LABEL,
  type ColumnFilterModel,
} from './filter-model'

export interface SetFilterPanelProps {
  /**
   * All known values for the column (Excel-style: the filter lists every value
   * present in the data). Order is preserved for display.
   */
  allValues: string[]
  /** Optional label lookup for values (falls back to the value itself). */
  labels?: FilterOption[]
  /** Current descriptor, or undefined when unfiltered (= all selected). */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  /**
   * Called after Enter commits the search. Hosts use this to close the popup;
   * omitting it leaves the panel open (the checkbox path never closes it).
   */
  onCommit?: () => void
}

/**
 * Excel / AG Grid-style set filter panel: a search box, a "(Select All)" toggle,
 * and a checkbox per value. All values checked ⇒ no filter (the descriptor is
 * cleared). Unchecking values narrows the filter to the checked subset. Reset
 * re-checks everything (removes the filter).
 *
 * The checked set is derived from the descriptor: no descriptor ⇒ all checked;
 * a `set` descriptor ⇒ exactly its `values` are checked.
 */
const SetFilterPanel = ({
  allValues,
  labels,
  value,
  onChange,
  onCommit,
}: SetFilterPanelProps) => {
  const [search, setSearch] = useState('')

  // Focus on mount so the user can type straight away. This relies on the host
  // popup setting `destroyOnHidden`: antd keeps popup content mounted by
  // default, and a kept-alive panel would focus only on the very first open
  // (which is also why the mount-only `autoFocus` prop was not enough).
  const searchRef = useRef<InputRef>(null)
  useEffect(() => {
    searchRef.current?.focus({ cursor: 'end' })
  }, [])

  const labelFor = useMemo(() => {
    const map = new Map<string, string>()
    for (const opt of labels ?? []) map.set(opt.value, opt.label)
    return (v: string) =>
      v === SET_FILTER_BLANK ? SET_FILTER_BLANK_LABEL : (map.get(v) ?? v)
  }, [labels])

  // Checked set: undefined descriptor ⇒ everything checked (unfiltered).
  const checked = useMemo(() => {
    if (value?.type === 'set') return new Set(value.values)
    return new Set(allValues)
  }, [value, allValues])

  const visibleValues = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return allValues
    return allValues.filter((v) => labelFor(v).toLowerCase().includes(q))
  }, [allValues, search, labelFor])

  const allChecked = allValues.length > 0 && checked.size === allValues.length
  const noneChecked = checked.size === 0
  const someChecked = !allChecked && !noneChecked

  /** Emit the checked set as a descriptor, collapsing "all checked" to no filter. */
  const emit = (nextChecked: Set<string>) => {
    if (nextChecked.size === allValues.length) {
      onChange(undefined)
      return
    }
    onChange({ type: 'set', values: Array.from(nextChecked) })
  }

  const toggleValue = (v: string, isChecked: boolean) => {
    const next = new Set(checked)
    if (isChecked) next.add(v)
    else next.delete(v)
    emit(next)
  }

  const toggleAll = (isChecked: boolean) => {
    emit(isChecked ? new Set(allValues) : new Set())
  }

  const reset = () => onChange(undefined)

  // Enter commits the search: the visible matches become the selection, the way
  // Excel's "filter to what I typed" works. Searching alone only narrows the
  // displayed list — it deliberately does not touch the descriptor, so without
  // this the typed query has no way to reach the grid.
  const commitSearch = () => {
    if (!search.trim() || visibleValues.length === 0) return
    emit(new Set(visibleValues))
    setSearch('')
    onCommit?.()
  }

  return (
    <div className={styles.panel}>
      <Input
        ref={searchRef}
        size="small"
        allowClear
        placeholder="Search..."
        prefix={<SearchOutlined />}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        onPressEnter={commitSearch}
        className={styles.search}
      />

      <div className={styles.list}>
        {!search && (
          <label className={styles.row}>
            <Checkbox
              checked={allChecked}
              indeterminate={someChecked}
              onChange={(e) => toggleAll(e.target.checked)}
            />
            <span className={styles.label}>(Select All)</span>
          </label>
        )}

        {visibleValues.map((v) => (
          <label key={v} className={styles.row}>
            <Checkbox
              checked={checked.has(v)}
              onChange={(e) => toggleValue(v, e.target.checked)}
            />
            <span className={styles.label}>{labelFor(v)}</span>
          </label>
        ))}

        {visibleValues.length === 0 && (
          <div className={styles.empty}>No matches</div>
        )}
      </div>

      <div className={styles.footer}>
        <Button size="small" type="text" onClick={reset} disabled={allChecked}>
          Reset
        </Button>
      </div>
    </div>
  )
}

export default SetFilterPanel
