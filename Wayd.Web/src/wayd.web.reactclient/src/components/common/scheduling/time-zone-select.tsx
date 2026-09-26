'use client'

import { Select, SelectProps } from 'antd'
import { TimeZoneDto } from '@/src/services/wayd-api'
import { useGetTimeZonesQuery } from '@/src/store/features/common/time-zones-api'

export const timeZoneLabel = (zone: TimeZoneDto) =>
  `(UTC${zone.currentOffset}) ${zone.id}`

export type TimeZoneSelectProps = Omit<
  SelectProps<string>,
  'options' | 'showSearch' | 'optionFilterProp' | 'loading'
>

/** A searchable select of the IANA time zones the server accepts. */
const TimeZoneSelect = (props: TimeZoneSelectProps) => {
  const { data: timeZones, isLoading } = useGetTimeZonesQuery()

  const options = (timeZones ?? []).map((zone) => ({
    value: zone.id,
    label: timeZoneLabel(zone),
  }))

  return (
    <Select
      placeholder="Select a time zone"
      {...props}
      showSearch
      options={options}
      optionFilterProp="label"
      loading={isLoading}
    />
  )
}

export default TimeZoneSelect
