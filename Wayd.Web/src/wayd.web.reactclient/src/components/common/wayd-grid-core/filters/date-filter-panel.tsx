'use client'

import { useMemo, useState } from 'react'
import { Button, Checkbox, Input } from 'antd'
import {
  DownOutlined,
  RightOutlined,
  SearchOutlined,
} from '@ant-design/icons'

import styles from './date-filter-panel.module.css'
import FilterPopup from './filter-popup'
import { buildDateTree, type YearNode } from './date-tree'
import {
  RELATIVE_DATE_OPTIONS,
  buildRelativeDateFilter,
  type RelativeDatePeriod,
} from './date-filter-relative'
import type { ColumnFilterModel } from './filter-model'

export interface DateFilterPanelProps {
  /** Distinct `YYYY-MM-DD` day keys present in the column (for the date tree). */
  dayKeys: string[]
  /** Current descriptor, or undefined when unfiltered. */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  /** Max conditions in the Custom Filter section. */
  maxConditions?: number
  /**
   * Expanded year/month node keys, controlled by the parent. Provide this (with
   * {@link onToggleNode}) to own the tree's expand state outside the panel — the
   * grid does this so expansion survives the popover content being rebuilt on a
   * filter change. When omitted, the panel manages its own expand state.
   */
  expandedNodes?: Set<string>
  onToggleNode?: (key: string) => void
}

/**
 * Excel-style date filter for a `date` column. Three sections over one
 * descriptor, last-updated wins:
 *  - **date tree** (set-style): checkbox tree of the distinct days present
 *    (Select All → Year → Month → Day) with search → emits a `dateSet`
 *    descriptor. All checked ⇒ no filter.
 *  - **Date Filters** (relative quick-filters): Today, This Week, Last Month, …
 *    → emits a concrete `date` range via {@link buildRelativeDateFilter}.
 *  - **Custom Filter**: the multi-condition {@link FilterPopup} (Equals/Before/
 *    After/Between/Blank) → emits a `date` conditions descriptor.
 *
 * Each section only reflects its own descriptor shape; the others read as
 * unfiltered, so switching sections replaces the previous filter (like the
 * combined text+set panel). The floating input remains a plain equals-only
 * DatePicker; this panel is what the filter icon opens.
 */
const DateFilterPanel = ({
  dayKeys,
  value,
  onChange,
  maxConditions,
  expandedNodes: expandedNodesProp,
  onToggleNode: onToggleNodeProp,
}: DateFilterPanelProps) => {
  const tree = useMemo(() => buildDateTree(dayKeys), [dayKeys])
  const allDayKeys = useMemo(
    () => tree.flatMap((y) => y.months.flatMap((m) => m.days.map((d) => d.key))),
    [tree],
  )

  const [search, setSearch] = useState('')
  const isDateSet = value?.type === 'dateSet'
  const isConditions = value?.type === 'date'
  const [quickOpen, setQuickOpen] = useState(false)
  const [customOpen, setCustomOpen] = useState(isConditions)

  // Expanded year/month node keys. Controlled by the parent when provided (the
  // grid owns this so expansion survives the popover content being rebuilt on a
  // filter change); otherwise fall back to internal state for standalone use.
  const [internalExpanded, setInternalExpanded] = useState<Set<string>>(
    new Set(),
  )
  const expandedNodes = expandedNodesProp ?? internalExpanded
  const toggleNode =
    onToggleNodeProp ??
    ((key: string) =>
      setInternalExpanded((prev) => {
        const next = new Set(prev)
        if (next.has(key)) next.delete(key)
        else next.add(key)
        return next
      }))

  // Checked day keys: no dateSet descriptor ⇒ everything checked (unfiltered).
  const checked = useMemo(() => {
    if (isDateSet) return new Set(value.values)
    return new Set(allDayKeys)
  }, [isDateSet, value, allDayKeys])

  /** Emit the checked set as a dateSet, collapsing "all checked" to no filter. */
  const emitChecked = (next: Set<string>) => {
    if (next.size === allDayKeys.length) {
      onChange(undefined)
      return
    }
    onChange({ type: 'dateSet', values: Array.from(next) })
  }

  const setKeys = (keys: string[], isChecked: boolean) => {
    const next = new Set(checked)
    for (const k of keys) {
      if (isChecked) next.add(k)
      else next.delete(k)
    }
    emitChecked(next)
  }

  const allChecked =
    allDayKeys.length > 0 && checked.size === allDayKeys.length
  const someChecked = checked.size > 0 && !allChecked

  const q = search.trim().toLowerCase()
  const matches = (key: string) => !q || key.includes(q)

  const pickRelative = (period: RelativeDatePeriod) => {
    onChange(buildRelativeDateFilter(period))
  }

  // Only the date-set descriptor drives the tree; the conditions descriptor is
  // fed to the Custom Filter section, which reads `date` and ignores `dateSet`.
  const conditionsValue = isConditions ? value : undefined

  return (
    <div className={styles.panel}>
      {/* ── Date tree (set-style) ── */}
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

      <div className={styles.tree}>
        {!search && allDayKeys.length > 0 && (
          <label className={styles.row}>
            <Checkbox
              checked={allChecked}
              indeterminate={someChecked}
              onChange={(e) => setKeys(allDayKeys, e.target.checked)}
            />
            <span className={styles.nodeLabel}>(Select All)</span>
          </label>
        )}

        {tree.map((year) => (
          <YearRow
            key={year.key}
            year={year}
            checked={checked}
            matches={matches}
            searching={!!q}
            expandedNodes={expandedNodes}
            onToggleNode={toggleNode}
            onToggle={setKeys}
          />
        ))}

        {allDayKeys.length === 0 && (
          <div className={styles.empty}>No dates</div>
        )}
        {allDayKeys.length > 0 &&
          q &&
          allDayKeys.filter(matches).length === 0 && (
            <div className={styles.empty}>No matches</div>
          )}
      </div>

      <div className={styles.treeFooter}>
        <Button
          size="small"
          type="text"
          onClick={() => onChange(undefined)}
          disabled={value === undefined}
        >
          Reset
        </Button>
      </div>

      {/* ── Date Filters (relative quick-filters) ── */}
      <button
        type="button"
        className={styles.sectionHeader}
        aria-expanded={quickOpen}
        onClick={() => setQuickOpen((prev) => !prev)}
      >
        <span>Date Filters</span>
        {quickOpen ? <DownOutlined /> : <RightOutlined />}
      </button>
      {quickOpen && (
        <div className={styles.quickList}>
          {RELATIVE_DATE_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              type="button"
              className={styles.quickItem}
              onClick={() => pickRelative(opt.value)}
            >
              {opt.label}
            </button>
          ))}
        </div>
      )}

      {/* ── Custom Filter (condition builder) ── */}
      <button
        type="button"
        className={styles.sectionHeader}
        aria-expanded={customOpen}
        onClick={() => setCustomOpen((prev) => !prev)}
      >
        <span>Custom Filter</span>
        {customOpen ? <DownOutlined /> : <RightOutlined />}
      </button>
      {customOpen && (
        <div className={styles.customSection}>
          <FilterPopup
            filterType="date"
            value={conditionsValue}
            onChange={onChange}
            maxConditions={maxConditions}
          />
        </div>
      )}
    </div>
  )
}

// ─── Tree rows ─────────────────────────────────────────────

interface YearRowProps {
  year: YearNode
  checked: Set<string>
  matches: (key: string) => boolean
  searching: boolean
  /** Set of expanded year/month node keys, owned by the panel. */
  expandedNodes: Set<string>
  onToggleNode: (key: string) => void
  onToggle: (keys: string[], isChecked: boolean) => void
}

/** A year node with its months; collapsible, with an indeterminate rollup. */
const YearRow = ({
  year,
  checked,
  matches,
  searching,
  expandedNodes,
  onToggleNode,
  onToggle,
}: YearRowProps) => {
  // When searching, only show months/days that contain a match, and auto-expand.
  const visibleMonths = year.months
    .map((m) => ({
      ...m,
      days: m.days.filter((d) => matches(d.key)),
    }))
    .filter((m) => m.days.length > 0)

  if (searching && visibleMonths.length === 0) return null

  const dayKeys = year.months.flatMap((m) => m.days.map((d) => d.key))
  const checkedCount = dayKeys.filter((k) => checked.has(k)).length
  const allOn = checkedCount === dayKeys.length
  const some = checkedCount > 0 && !allOn

  const expanded = expandedNodes.has(year.key) || searching

  return (
    <div>
      <div className={styles.row}>
        <span
          role="button"
          aria-label={expanded ? 'Collapse' : 'Expand'}
          className={styles.expander}
          onClick={() => onToggleNode(year.key)}
        >
          {expanded ? <DownOutlined /> : <RightOutlined />}
        </span>
        <Checkbox
          checked={allOn}
          indeterminate={some}
          onChange={(e) => onToggle(dayKeys, e.target.checked)}
        />
        <span className={styles.nodeLabel}>{year.label}</span>
      </div>

      {expanded && (
        <div className={styles.indent}>
          {visibleMonths.map((month) => (
            <MonthRow
              key={month.key}
              month={month}
              checked={checked}
              searching={searching}
              expandedNodes={expandedNodes}
              onToggleNode={onToggleNode}
              onToggle={onToggle}
            />
          ))}
        </div>
      )}
    </div>
  )
}

interface MonthRowProps {
  month: YearNode['months'][number]
  checked: Set<string>
  searching: boolean
  expandedNodes: Set<string>
  onToggleNode: (key: string) => void
  onToggle: (keys: string[], isChecked: boolean) => void
}

/** A month node with its days; collapsible, with an indeterminate rollup. */
const MonthRow = ({
  month,
  checked,
  searching,
  expandedNodes,
  onToggleNode,
  onToggle,
}: MonthRowProps) => {
  const dayKeys = month.days.map((d) => d.key)
  const checkedCount = dayKeys.filter((k) => checked.has(k)).length
  const allOn = checkedCount === dayKeys.length
  const some = checkedCount > 0 && !allOn

  const expanded = expandedNodes.has(month.key) || searching

  return (
    <div>
      <div className={styles.row}>
        <span
          role="button"
          aria-label={expanded ? 'Collapse' : 'Expand'}
          className={styles.expander}
          onClick={() => onToggleNode(month.key)}
        >
          {expanded ? <DownOutlined /> : <RightOutlined />}
        </span>
        <Checkbox
          checked={allOn}
          indeterminate={some}
          onChange={(e) => onToggle(dayKeys, e.target.checked)}
        />
        <span className={styles.nodeLabel}>{month.label}</span>
      </div>

      {expanded && (
        <div className={styles.indent}>
          {month.days.map((day) => (
            <label key={day.key} className={styles.row}>
              <span className={styles.expanderSpacer} />
              <Checkbox
                checked={checked.has(day.key)}
                onChange={(e) => onToggle([day.key], e.target.checked)}
              />
              <span className={styles.nodeLabel}>{day.label}</span>
            </label>
          ))}
        </div>
      )}
    </div>
  )
}

export default DateFilterPanel
