'use client'

import { Button, DatePicker, Input, InputNumber, Segmented, Select } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'

import styles from './filter-popup.module.css'
import {
  addCondition,
  createEmptyFilterModel,
  operatorNeedsSecondValue,
  operatorNeedsValue,
  operatorOptionsFor,
  removeConditionAt,
  updateConditionAt,
  CONDITION_LIMIT,
  DEFAULT_MAX_CONDITIONS,
  type ColumnFilterModel,
  type ConditionModel,
  type DateCondition,
  type DateTimeCondition,
  type FilterType,
  type NumberCondition,
  type TextCondition,
} from './filter-model'

export interface FilterPopupProps {
  filterType: FilterType
  /** Current descriptor, or undefined when the column is unfiltered. */
  value: ColumnFilterModel | undefined
  onChange: (next: ColumnFilterModel | undefined) => void
  /** Max conditions for text/number/date/dateTime popups (default 5 — the max; set lower to restrict). */
  maxConditions?: number
}

const JOIN_OPTIONS = [
  { label: 'AND', value: 'AND' },
  { label: 'OR', value: 'OR' },
]

/**
 * Per-column filter popup — the "complex" filter layer for condition-based
 * filters (text/number/date/dateTime): multiple conditions (up to
 * `maxConditions`) joined by AND/OR. Changes apply live (the grid debounces text
 * input upstream). Set columns use {@link SetFilterPanel} instead.
 */
const FilterPopup = ({
  filterType,
  value,
  onChange,
  maxConditions = DEFAULT_MAX_CONDITIONS,
}: FilterPopupProps) => {
  const cap = Math.min(Math.max(maxConditions, 1), CONDITION_LIMIT)

  const emit = (next: ColumnFilterModel) => onChange(next)
  const clear = () => onChange(undefined)

  // ─── Condition-based filters ─────────────────────────────
  const model: ConditionModel =
    value && value.type === filterType
      ? (value as ConditionModel)
      : (createEmptyFilterModel(filterType) as ConditionModel)

  const opOptions = operatorOptionsFor(filterType)

  const change = (next: ConditionModel) => emit(next)

  const renderValueInputs = (
    condition: ConditionModel['conditions'][number],
    index: number,
  ) => {
    if (!operatorNeedsValue(condition.op)) return null
    const needsSecond = operatorNeedsSecondValue(condition.op)

    if (filterType === 'text') {
      const c = condition as TextCondition
      return (
        <Input
          size="small"
          allowClear
          placeholder="Value"
          className={styles.valueInput}
          value={c.value}
          onChange={(e) =>
            change(updateConditionAt(model, index, { value: e.target.value }))
          }
        />
      )
    }

    if (filterType === 'number') {
      const c = condition as NumberCondition
      return (
        <div className={styles.rangeRow}>
          <InputNumber
            size="small"
            placeholder={needsSecond ? 'From' : 'Value'}
            className={styles.valueInput}
            value={c.value}
            onChange={(v) =>
              change(updateConditionAt(model, index, { value: v ?? null }))
            }
          />
          {needsSecond && (
            <InputNumber
              size="small"
              placeholder="To"
              className={styles.valueInput}
              value={c.valueTo ?? null}
              onChange={(v) =>
                change(updateConditionAt(model, index, { valueTo: v ?? null }))
              }
            />
          )}
        </div>
      )
    }

    // date / dateTime
    const c = condition as DateCondition | DateTimeCondition
    const showTime = filterType === 'dateTime'
    const toIso = (d: dayjs.Dayjs | null) =>
      d ? (showTime ? d.toISOString() : d.format('YYYY-MM-DD')) : null

    return (
      <div className={styles.rangeRow}>
        <DatePicker
          size="small"
          showTime={showTime}
          className={styles.valueInput}
          value={c.value ? dayjs(c.value) : null}
          onChange={(d) =>
            change(updateConditionAt(model, index, { value: toIso(d) }))
          }
        />
        {needsSecond && (
          <DatePicker
            size="small"
            showTime={showTime}
            className={styles.valueInput}
            value={c.valueTo ? dayjs(c.valueTo) : null}
            onChange={(d) =>
              change(updateConditionAt(model, index, { valueTo: toIso(d) }))
            }
          />
        )}
      </div>
    )
  }

  return (
    <div className={styles.popup}>
      {model.conditions.map((condition, index) => (
        <div key={index} className={styles.conditionBlock}>
          {index > 0 && (
            <Segmented
              size="small"
              className={styles.join}
              options={JOIN_OPTIONS}
              value={model.join}
              onChange={(join) =>
                change({ ...model, join: join as ConditionModel['join'] })
              }
            />
          )}
          <div className={styles.conditionRow}>
            <Select
              size="small"
              className={styles.opSelect}
              options={opOptions}
              value={condition.op}
              popupMatchSelectWidth={false}
              onChange={(op) =>
                change(updateConditionAt(model, index, { op } as any))
              }
            />
            {model.conditions.length > 1 && (
              <Button
                size="small"
                type="text"
                icon={<DeleteOutlined />}
                aria-label="Remove condition"
                onClick={() => change(removeConditionAt(model, index))}
              />
            )}
          </div>
          {renderValueInputs(condition, index)}
        </div>
      ))}

      <div className={styles.footer}>
        {model.conditions.length < cap ? (
          <Button
            size="small"
            type="text"
            icon={<PlusOutlined />}
            onClick={() => change(addCondition(model, cap))}
          >
            Add condition
          </Button>
        ) : (
          // Spacer keeps Clear right-aligned when Add condition is hidden.
          <span />
        )}
        <Button size="small" type="text" onClick={clear}>
          Clear
        </Button>
      </div>
    </div>
  )
}

export default FilterPopup
