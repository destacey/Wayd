'use client'

import { useEffect, useRef, useState } from 'react'
import { Button, Checkbox, Input, Spin, type InputRef } from 'antd'
import { SearchOutlined } from '@ant-design/icons'

import styles from './set-filter-panel.module.css'
import type {
  FilterOption,
  FilterOptionsPage,
  FilterOptionsSource,
} from '../types'
import {
  SET_FILTER_BLANK,
  SET_FILTER_BLANK_LABEL,
  type ColumnFilterModel,
} from './filter-model'

/** How long the panel waits after typing before it asks for values again. */
export const REMOTE_SET_FILTER_DEBOUNCE_MS = 250

export interface RemoteSetFilterPanelProps {
  /** Where the values come from. */
  source: FilterOptionsSource
  /** Current descriptor, or undefined when unfiltered (= nothing picked). */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  /**
   * Called after Enter picks the search's matches. Hosts use this to close the
   * popup; omitting it leaves the panel open.
   */
  onCommit?: () => void
}

/** The answer for one search, kept with the search it answers. */
interface Loaded {
  search: string
  page: FilterOptionsPage
}

/**
 * A set filter over values loaded from a {@link FilterOptionsSource}: a search
 * box, the values picked so far, and the values matching the search. It works
 * by inclusion — nothing is checked until the user picks a value — because a
 * column whose values are not all known cannot express "every value but these".
 *
 * Picked values stay listed, with their labels, whatever the search returns, so
 * a narrowed search never hides what the filter holds.
 */
const RemoteSetFilterPanel = ({
  source,
  value,
  onChange,
  onCommit,
}: RemoteSetFilterPanelProps) => {
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [failedQuery, setFailedQuery] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  // A host may build its columns, and so the source, on every render; reading
  // the latest load through a ref keeps that from refetching each time.
  const loadRef = useRef(source.load)
  useEffect(() => {
    loadRef.current = source.load
  })

  // Focus on mount, which relies on the host popup setting `destroyOnHidden`
  // (see SetFilterPanel).
  const searchRef = useRef<InputRef>(null)
  useEffect(() => {
    searchRef.current?.focus({ cursor: 'end' })
  }, [])

  useEffect(() => {
    const trimmed = search.trim()
    if (trimmed === query) return
    const timer = setTimeout(
      () => setQuery(trimmed),
      REMOTE_SET_FILTER_DEBOUNCE_MS,
    )
    return () => clearTimeout(timer)
  }, [search, query])

  useEffect(() => {
    const controller = new AbortController()
    loadRef.current({ search: query, signal: controller.signal }).then(
      (page) => {
        if (controller.signal.aborted) return
        setLoaded({ search: query, page })
        setFailedQuery(null)
      },
      () => {
        if (!controller.signal.aborted) setFailedQuery(query)
      },
    )
    return () => controller.abort()
  }, [query, attempt])

  const picked = value?.type === 'set' ? value.values : []
  const pickedLabels = value?.type === 'set' ? (value.labels ?? {}) : {}
  const pickedSet = new Set(picked)

  const current =
    loaded && loaded.search === query && search.trim() === query
      ? loaded.page
      : null
  const failed = failedQuery === query && search.trim() === query
  const loading = !current && !failed

  const loadedLabel = (v: string) =>
    loaded?.page.options.find((o) => o.value === v)?.label
  const labelFor = (v: string) =>
    v === SET_FILTER_BLANK
      ? SET_FILTER_BLANK_LABEL
      : (pickedLabels[v] ?? loadedLabel(v) ?? v)

  const offerBlank =
    !!source.includeBlank &&
    SET_FILTER_BLANK_LABEL.toLowerCase().includes(search.trim().toLowerCase())
  const matches: FilterOption[] = current
    ? [
        ...(offerBlank
          ? [{ value: SET_FILTER_BLANK, label: SET_FILTER_BLANK_LABEL }]
          : []),
        ...current.options,
      ].filter((o) => !pickedSet.has(o.value))
    : []

  /** Emits the picked values with their labels; none picked is no filter. */
  const emit = (values: string[], labels: Record<string, string>) => {
    if (values.length === 0) {
      onChange(undefined)
      return
    }
    const kept: Record<string, string> = {}
    for (const v of values) if (labels[v] !== undefined) kept[v] = labels[v]
    onChange({ type: 'set', values, labels: kept })
  }

  const toggle = (option: FilterOption, isChecked: boolean) => {
    if (isChecked) {
      emit([...picked, option.value], {
        ...pickedLabels,
        [option.value]: option.label,
      })
    } else {
      emit(
        picked.filter((v) => v !== option.value),
        pickedLabels,
      )
    }
  }

  // Enter picks every value the search shows, the way the plain set filter's
  // Enter filters to what was typed.
  const commitSearch = () => {
    if (!search.trim() || matches.length === 0) return
    const labels = { ...pickedLabels }
    for (const o of matches) labels[o.value] = o.label
    emit([...picked, ...matches.map((o) => o.value)], labels)
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

      <div className={styles.list} aria-busy={loading}>
        {picked.map((v) => (
          <label key={v} className={styles.row}>
            <Checkbox
              checked
              onChange={() => toggle({ value: v, label: labelFor(v) }, false)}
            />
            <span className={styles.label}>{labelFor(v)}</span>
          </label>
        ))}

        {picked.length > 0 && (matches.length > 0 || loading || failed) && (
          <div className={styles.divider} role="separator" />
        )}

        {matches.map((o) => (
          <label key={o.value} className={styles.row}>
            <Checkbox onChange={() => toggle(o, true)} />
            <span className={styles.label}>{o.label}</span>
          </label>
        ))}

        {loading && (
          <div className={styles.empty}>
            <Spin size="small" /> Loading values…
          </div>
        )}

        {failed && (
          <div className={styles.empty}>
            Could not load the values.{' '}
            <Button
              size="small"
              type="link"
              onClick={() => setAttempt((n) => n + 1)}
            >
              Retry
            </Button>
          </div>
        )}

        {current && matches.length === 0 && picked.length === 0 && (
          <div className={styles.empty}>No matches</div>
        )}

        {current?.hasMore && (
          <div className={styles.empty}>
            Showing the first {current.options.length}. Type to narrow.
          </div>
        )}
      </div>

      <div className={styles.footer}>
        <Button
          size="small"
          type="text"
          onClick={() => onChange(undefined)}
          disabled={picked.length === 0}
        >
          Reset
        </Button>
      </div>
    </div>
  )
}

export default RemoteSetFilterPanel
