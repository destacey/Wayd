'use client'

import useAuth from '@/src/components/contexts/auth'
import { NavigationDto } from '@/src/services/wayd-api'
import { useGetHolidayCalendarsQuery } from '@/src/store/features/organization/holiday-calendars-api'
import { Select } from 'antd'

export interface HolidayCalendarSelectProps {
  value?: string | null
  onChange?: (calendarId: string | undefined) => void
  /** What no calendar means where the select is used, e.g. "System default". */
  emptyLabel: string
  /**
   * The calendar the record holds now, from the record itself, so its name
   * shows even when the calendars cannot be listed.
   */
  current?: NavigationDto
  'aria-label'?: string
}

/**
 * A select of the holiday calendars, where clearing it means `emptyLabel`. Listing the calendars needs
 * `Permissions.HolidayCalendars.View`; without it the select is disabled and shows `current` by name.
 */
const HolidayCalendarSelect = ({
  value,
  onChange,
  emptyLabel,
  current,
  'aria-label': ariaLabel,
}: HolidayCalendarSelectProps) => {
  const { hasPermissionClaim } = useAuth()
  const canView = hasPermissionClaim('Permissions.HolidayCalendars.View')
  const { data: calendars, isLoading } = useGetHolidayCalendarsQuery(
    undefined,
    { skip: !canView },
  )

  return (
    <Select
      value={value ?? undefined}
      onChange={(id) => onChange?.(id ?? undefined)}
      allowClear
      showSearch
      optionFilterProp="label"
      loading={isLoading}
      disabled={!canView}
      placeholder={emptyLabel}
      aria-label={ariaLabel}
      options={[
        ...(calendars ?? []).map((c) => ({
          value: c.id,
          label: c.isDefault ? `${c.name} (default)` : c.name,
        })),
        ...(current && !calendars?.some((c) => c.id === current.id)
          ? [{ value: current.id, label: current.name }]
          : []),
      ]}
    />
  )
}

export default HolidayCalendarSelect
