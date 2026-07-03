'use client'

import { useMemo, useState } from 'react'
import { Button, Checkbox, Input } from 'antd'
import { SearchOutlined } from '@ant-design/icons'

import styles from './set-filter-panel.module.css'
import type { FilterOption } from '../types'
import type { ColumnFilterModel } from './filter-model'

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
}: SetFilterPanelProps) => {
  const [search, setSearch] = useState('')

  const labelFor = useMemo(() => {
    const map = new Map<string, string>()
    for (const opt of labels ?? []) map.set(opt.value, opt.label)
    return (v: string) => map.get(v) ?? v
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

  return (
    <div className={styles.panel}>
      <Input
        size="small"
        allowClear
        autoFocus
        placeholder="Search..."
        prefix={<SearchOutlined />}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
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
