'use client'

import { Button, Flex } from 'antd'
import { ProductStatusAlias } from '@/src/services/wayd-api'
import styles from './incident-filter-bar.module.css'

export interface IncidentFilterOption<T extends string> {
  value: T
  label: string
  /** The severity's own color, used as the lit chip's edge. */
  color?: string | null
}

export interface IncidentFilterBarProps {
  statuses: IncidentFilterOption<ProductStatusAlias>[]
  selectedStatuses: ProductStatusAlias[]
  onStatusesChange: (statuses: ProductStatusAlias[]) => void
  severities: IncidentFilterOption<string>[]
  selectedSeverities: string[]
  onSeveritiesChange: (severityLevelIds: string[]) => void
}

const toggle = <T extends string>(
  selected: T[],
  options: IncidentFilterOption<T>[],
  value: T,
): T[] => {
  // An empty selection means every option. Turning one off narrows to the rest.
  const effective =
    selected.length === 0 ? options.map((option) => option.value) : selected
  const next = effective.includes(value)
    ? effective.filter((item) => item !== value)
    : [...effective, value]
  return next.length === options.length ? [] : next
}

const ChipRow = <T extends string>({
  label,
  options,
  selected,
  onChange,
}: {
  label: string
  options: IncidentFilterOption<T>[]
  selected: T[]
  onChange: (next: T[]) => void
}) => (
  <Flex gap={8} align="center" wrap>
    <span className={styles.label}>{label}</span>
    {options.map((option) => {
      const isSelected = selected.length === 0 || selected.includes(option.value)
      return (
        <Button
          key={option.value}
          size="small"
          className={styles.chip}
          color={isSelected && !option.color ? 'primary' : 'default'}
          variant="outlined"
          style={
            isSelected
              ? option.color
                ? { borderColor: option.color, color: option.color }
                : undefined
              : { borderStyle: 'dashed' }
          }
          onClick={() => onChange(toggle(selected, options, option.value))}
        >
          {option.label}
        </Button>
      )
    })}
  </Flex>
)

/**
 * The incidents list's common filters, kept above the grid.
 *
 * State and severity are what someone opens the list to narrow. An empty
 * selection asks for every value, so every chip stays lit until one is turned off.
 */
const IncidentFilterBar = ({
  statuses,
  selectedStatuses,
  onStatusesChange,
  severities,
  selectedSeverities,
  onSeveritiesChange,
}: IncidentFilterBarProps) => (
  <div className={styles.bar}>
    <ChipRow
      label="State"
      options={statuses}
      selected={selectedStatuses}
      onChange={onStatusesChange}
    />
    {severities.length > 0 ? (
      <ChipRow
        label="Severity"
        options={severities}
        selected={selectedSeverities}
        onChange={onSeveritiesChange}
      />
    ) : null}
  </div>
)

export default IncidentFilterBar
