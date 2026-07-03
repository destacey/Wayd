'use client'

import { useState } from 'react'
import { DownOutlined, RightOutlined } from '@ant-design/icons'

import styles from './combined-filter-panel.module.css'
import type { FilterOption } from '../types'
import FilterPopup from './filter-popup'
import SetFilterPanel from './set-filter-panel'
import type { ColumnFilterModel } from './filter-model'

export interface CombinedFilterPanelProps {
  /** All known values for the set section (from faceted data / filterOptions). */
  allValues: string[]
  /** Optional label lookup for set values. */
  labels?: FilterOption[]
  /** Current descriptor, or undefined when unfiltered. */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  /** Max text conditions in the Text Filter section. */
  maxConditions?: number
}

/**
 * Combined text + set filter (AG Grid / Excel style) for string columns. A
 * collapsible **Text Filter** section (multi-condition Contains/Equals/… via
 * {@link FilterPopup}) sits above the **set** section (search + Select All +
 * checkboxes, via {@link SetFilterPanel}).
 *
 * One descriptor per column, last-updated wins: editing the text section emits a
 * `text` descriptor (replacing any set selection); (un)checking set values emits
 * a `set` descriptor (replacing any text). Each section only shows its own state
 * — the other reads as unfiltered — so switching sides clears the previous.
 */
const CombinedFilterPanel = ({
  allValues,
  labels,
  value,
  onChange,
  maxConditions,
}: CombinedFilterPanelProps) => {
  const isText = value?.type === 'text'
  // Auto-expand the text section when a text filter is already active (e.g. the
  // user typed in the floating input), so it's visible on open.
  const [textOpen, setTextOpen] = useState(isText)

  // Feed each section only its matching descriptor; the other sees `undefined`
  // (= unfiltered) so it renders a clean default.
  const textValue = isText ? value : undefined
  const setValue = value?.type === 'set' ? value : undefined

  return (
    <div className={styles.panel}>
      <button
        type="button"
        className={styles.textHeader}
        aria-expanded={textOpen}
        onClick={() => setTextOpen((prev) => !prev)}
      >
        <span>Text Filter</span>
        {textOpen ? <DownOutlined /> : <RightOutlined />}
      </button>

      {textOpen && (
        <div className={styles.textSection}>
          <FilterPopup
            filterType="text"
            value={textValue}
            onChange={onChange}
            maxConditions={maxConditions}
          />
        </div>
      )}

      <div className={styles.setSection}>
        <SetFilterPanel
          allValues={allValues}
          labels={labels}
          value={setValue}
          onChange={onChange}
        />
      </div>
    </div>
  )
}

export default CombinedFilterPanel
