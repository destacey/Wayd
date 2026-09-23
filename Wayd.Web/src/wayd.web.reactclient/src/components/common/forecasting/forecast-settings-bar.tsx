'use client'

import { DatePicker, Flex, Select, Switch, Typography } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC } from 'react'

const { Text } = Typography

export const LOOKBACK_DAY_OPTIONS = [30, 60, 90, 120, 180]
export const DEFAULT_LOOKBACK_DAYS = 90

/** The API forecasts at most this far ahead. */
export const MAX_FORECAST_DAYS = 730

export interface ForecastSettings {
  lookbackDays: number
  /** Unset uses the record's own date, when it has one. */
  targetDate?: Dayjs
  ignoreDependencies: boolean
}

export const DEFAULT_FORECAST_SETTINGS: ForecastSettings = {
  lookbackDays: DEFAULT_LOOKBACK_DAYS,
  ignoreDependencies: false,
}

export interface ForecastSettingsBarProps {
  value: ForecastSettings
  onChange: (value: ForecastSettings) => void
  /**
   * Label for the target date picker, or omit to hide it. Its placeholder
   * names the date used when none is picked.
   */
  targetDateLabel?: string
  targetDatePlaceholder?: string
  /** A target date is required, as on the team throughput forecast. */
  targetDateRequired?: boolean
  showIgnoreDependencies?: boolean
}

const isOutOfRange = (date: Dayjs) =>
  date.isBefore(dayjs(), 'day') ||
  !date.isBefore(dayjs().add(MAX_FORECAST_DAYS, 'day'), 'day')

/**
 * Per-request forecast choices. Nothing here is saved: each defaults to the
 * standard forecast, and the report states which were used.
 */
const ForecastSettingsBar: FC<ForecastSettingsBarProps> = ({
  value,
  onChange,
  targetDateLabel,
  targetDatePlaceholder,
  targetDateRequired = false,
  showIgnoreDependencies = false,
}) => (
  <Flex gap="middle" align="center" wrap>
    {targetDateLabel && (
      <Flex gap="small" align="center">
        <Text>{targetDateLabel}</Text>
        <DatePicker
          value={value.targetDate}
          allowClear={!targetDateRequired}
          placeholder={targetDatePlaceholder}
          disabledDate={isOutOfRange}
          onChange={(date) => {
            if (targetDateRequired && !date) return
            onChange({ ...value, targetDate: date ?? undefined })
          }}
        />
      </Flex>
    )}
    <Flex gap="small" align="center">
      <Text>History</Text>
      <Select
        value={value.lookbackDays}
        onChange={(lookbackDays) => onChange({ ...value, lookbackDays })}
        options={LOOKBACK_DAY_OPTIONS.map((days) => ({
          value: days,
          label: `Last ${days} days`,
        }))}
        style={{ width: 140 }}
      />
    </Flex>
    {showIgnoreDependencies && (
      <Flex gap="small" align="center">
        <Switch
          size="small"
          checked={value.ignoreDependencies}
          onChange={(ignoreDependencies) =>
            onChange({ ...value, ignoreDependencies })
          }
        />
        <Text>Ignore dependencies</Text>
      </Flex>
    )}
  </Flex>
)

export default ForecastSettingsBar
