'use client'

import {
  IntakeChannelDto,
  IntakeDeskDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { FilterOutlined } from '@ant-design/icons'
import { Badge, Button, Drawer, Flex, Grid, Segmented, Select } from 'antd'
import { FC, ReactNode, useState } from 'react'
import styles from './request-filter-bar.module.css'
import {
  AssigneeFilter,
  DEFAULT_REQUEST_FILTERS,
  RequestFilters,
  RequestScope,
  activeFilterCount,
} from './request-filters'

const { useBreakpoint } = Grid

export interface RequestFilterBarProps {
  filters: RequestFilters
  onChange: (filters: RequestFilters) => void
  /** The requests in scope, before the other filters: where the status names and counts come from. */
  requests: IntakeRequestSummaryDto[] | undefined
  desks: IntakeDeskDto[] | undefined
  channels: IntakeChannelDto[] | undefined
  /** Whether the reader has an employee record, without which nothing is assigned to them. */
  hasLinkedEmployee: boolean
  /** Shown first in the row on every screen, such as the list and board toggle. */
  leading?: ReactNode
}

const countBy = (
  requests: IntakeRequestSummaryDto[] | undefined,
  key: (r: IntakeRequestSummaryDto) => string,
) => {
  const counts = new Map<string, number>()
  for (const r of requests ?? [])
    counts.set(key(r), (counts.get(key(r)) ?? 0) + 1)
  return counts
}

const FilterControls: FC<RequestFilterBarProps & { stacked: boolean }> = ({
  filters,
  onChange,
  requests,
  desks,
  channels,
  hasLinkedEmployee,
  stacked,
}) => {
  const set = (patch: Partial<RequestFilters>) =>
    onChange({ ...filters, ...patch })

  // Counts are of the requests the reader can see, so no number hints at a request kept from them.
  const perDesk = countBy(requests, (r) => r.desk.id)
  const perStatus = countBy(requests, (r) => r.status.name)
  const controlClass = stacked ? styles.stackedControl : styles.control

  return (
    <>
      <Segmented<RequestScope>
        className={stacked ? styles.stackedControl : undefined}
        block={stacked}
        value={filters.scope}
        onChange={(scope) => set({ scope })}
        options={[
          { label: 'Open', value: 'open' },
          { label: 'Everything', value: 'all' },
        ]}
        aria-label="Which requests"
      />
      <Select
        className={controlClass}
        mode="multiple"
        allowClear
        maxTagCount="responsive"
        placeholder="All desks"
        aria-label="Desk"
        value={filters.deskIds}
        onChange={(deskIds) => set({ deskIds })}
        optionFilterProp="label"
        options={[...(desks ?? [])]
          .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
          .map((d) => ({
            value: d.id,
            label: `${d.name} (${perDesk.get(d.id) ?? 0})`,
          }))}
      />
      <Select
        className={controlClass}
        mode="multiple"
        allowClear
        maxTagCount="responsive"
        placeholder="Any status"
        aria-label="Status"
        value={filters.statusNames}
        onChange={(statusNames) => set({ statusNames })}
        options={[...perStatus.keys()]
          .sort(caseInsensitiveCompare)
          .map((name) => ({
            value: name,
            label: `${name} (${perStatus.get(name)})`,
          }))}
      />
      <Select<AssigneeFilter>
        className={controlClass}
        aria-label="Assignee"
        value={filters.assignee}
        onChange={(assignee) => set({ assignee })}
        options={[
          { value: 'anyone', label: 'Anyone assigned or not' },
          ...(hasLinkedEmployee
            ? [{ value: 'me' as const, label: 'Assigned to me' }]
            : []),
          { value: 'unassigned', label: 'Not assigned' },
        ]}
      />
      <Select
        className={controlClass}
        mode="multiple"
        allowClear
        maxTagCount="responsive"
        placeholder="Any channel"
        aria-label="Channel"
        value={filters.channelIds}
        onChange={(channelIds) => set({ channelIds })}
        optionFilterProp="label"
        options={(channels ?? []).map((c) => ({ value: c.id, label: c.name }))}
      />
    </>
  )
}

/**
 * The Requests page's filters, shared by the list and the board. Laid out in a row that wraps above the
 * requests on wide and tablet screens; on a phone they fold into a Filters button, so the requests stay
 * the first thing on screen.
 */
const RequestFilterBar: FC<RequestFilterBarProps> = (props) => {
  const screens = useBreakpoint()
  const [open, setOpen] = useState(false)
  const active = activeFilterCount(props.filters)

  if (screens.md !== false) {
    return (
      <Flex wrap gap={8} align="center" className={styles.bar}>
        {props.leading}
        <FilterControls {...props} stacked={false} />
        {active > 0 && (
          <Button
            type="link"
            onClick={() => props.onChange(DEFAULT_REQUEST_FILTERS)}
          >
            Clear filters
          </Button>
        )}
      </Flex>
    )
  }

  return (
    <Flex gap={8} align="center" className={styles.bar}>
      {props.leading}
      <Badge count={active} size="small">
        <Button
          icon={<FilterOutlined />}
          className={styles.touch}
          onClick={() => setOpen(true)}
        >
          Filters
        </Button>
      </Badge>
      <Drawer
        title="Filter requests"
        placement="bottom"
        size="auto"
        open={open}
        onClose={() => setOpen(false)}
        extra={
          active > 0 ? (
            <Button
              type="link"
              onClick={() => props.onChange(DEFAULT_REQUEST_FILTERS)}
            >
              Clear
            </Button>
          ) : null
        }
      >
        <Flex vertical gap={12}>
          <FilterControls {...props} stacked />
        </Flex>
      </Drawer>
    </Flex>
  )
}

export default RequestFilterBar
