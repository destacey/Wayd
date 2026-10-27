'use client'

import {
  NOT_RECORDED_CHANNEL,
  NOT_RECORDED_CHANNEL_LABEL,
} from '../../_components/channel'
import {
  IntakeChannelDto,
  IntakeDeskDto,
  IntakeRequestSummaryDto,
  IntakeRequestTagCategoryDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { WaydTooltip } from '@/src/components/common'
import { ClearOutlined, FilterOutlined } from '@ant-design/icons'
import {
  Badge,
  Button,
  ConfigProvider,
  Drawer,
  Flex,
  Grid,
  Segmented,
  Select,
} from 'antd'
import { FC, useState } from 'react'
import { TOUCH_THEME } from '../../_components/touch-theme'
import styles from './request-filter-bar.module.css'
import {
  AssigneeFilter,
  BUILT_IN_VIEWS,
  DEFAULT_REQUEST_FILTERS,
  MY_DESKS,
  NeedByFilter,
  RequestFilters,
  RequestScope,
  activeFilterCount,
  applyRequestFilters,
  matchingView,
} from './request-filters'

const { useBreakpoint } = Grid

export interface RequestFilterBarProps {
  filters: RequestFilters
  onChange: (filters: RequestFilters) => void
  /** The requests in scope, before the other filters: where the status names and counts come from. */
  requests: IntakeRequestSummaryDto[] | undefined
  desks: IntakeDeskDto[] | undefined
  channels: IntakeChannelDto[] | undefined
  /** The tag axes, inactive tags included, since requests keep them. */
  tagCategories: IntakeRequestTagCategoryDto[] | undefined
  /** The reader's employee record, without which nothing is assigned to them. */
  employeeId: string | null | undefined
  /** The desks the reader works, for "My desks"; empty for someone who works none. */
  myDeskIds: string[]
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

/** Puts every filter back to its default, the same control the other filter bars use. */
const ResetFilters: FC<{ onReset: () => void }> = ({ onReset }) => (
  <WaydTooltip title="Reset Filters">
    <Button
      type="text"
      shape="circle"
      icon={<ClearOutlined />}
      aria-label="Reset filters"
      onClick={onReset}
    />
  </WaydTooltip>
)

const FilterControls: FC<RequestFilterBarProps & { stacked: boolean }> = ({
  filters,
  onChange,
  requests,
  desks,
  channels,
  tagCategories,
  employeeId,
  myDeskIds,
  stacked,
}) => {
  const set = (patch: Partial<RequestFilters>) =>
    onChange({ ...filters, ...patch })

  // Counts are of the requests the reader can see, so no number hints at a request kept from them.
  const perDesk = countBy(requests, (r) => r.desk.id)
  const perStatus = countBy(requests, (r) => r.status.name)
  const perTag = new Map<string, number>()
  for (const r of requests ?? [])
    for (const t of r.tags) perTag.set(t.id, (perTag.get(t.id) ?? 0) + 1)
  const controlClass = stacked ? styles.stackedControl : styles.control
  const current = matchingView(filters)

  return (
    <>
      <Select
        className={controlClass}
        aria-label="View"
        placeholder="Custom view"
        value={current?.id}
        onChange={(id) => {
          const view = BUILT_IN_VIEWS.find((v) => v.id === id)
          if (view) onChange(view.filters)
        }}
        options={BUILT_IN_VIEWS.filter(
          (v) =>
            (employeeId || v.filters.assignee !== 'me') &&
            (myDeskIds.length > 0 || !v.filters.deskIds.includes(MY_DESKS)),
        ).map((v) => ({
          value: v.id,
          // Every built-in view is of open requests, so it can be counted only from the open set.
          label:
            filters.scope === 'open' && requests
              ? `${v.name} (${applyRequestFilters(requests, v.filters, employeeId, myDeskIds).length})`
              : v.name,
          title: v.description,
        }))}
      />
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
        options={[
          ...(myDeskIds.length > 0
            ? [
                {
                  value: MY_DESKS,
                  label: `Desks I work (${myDeskIds.reduce((n, id) => n + (perDesk.get(id) ?? 0), 0)})`,
                },
              ]
            : []),
          ...[...(desks ?? [])]
            .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
            .map((d) => ({
              value: d.id,
              label: `${d.name} (${perDesk.get(d.id) ?? 0})`,
            })),
        ]}
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
          ...(employeeId
            ? [{ value: 'me' as const, label: 'Assigned to me' }]
            : []),
          { value: 'unassigned', label: 'Not assigned' },
        ]}
      />
      <Select<NeedByFilter>
        className={controlClass}
        aria-label="Need by"
        value={filters.needBy}
        onChange={(needBy) => set({ needBy })}
        options={[
          { value: 'any', label: 'Any need-by date' },
          { value: 'fixed', label: 'Fixed for us' },
          { value: 'chosen', label: 'We chose it' },
          { value: 'none', label: 'No date given' },
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
        options={[
          ...(channels ?? []).map((c) => ({
            value: c.id,
            label: c.isActive ? c.name : `${c.name} (Archived)`,
          })),
          { value: NOT_RECORDED_CHANNEL, label: NOT_RECORDED_CHANNEL_LABEL },
        ]}
      />
      {(tagCategories?.length ?? 0) > 0 && (
        <Select
          className={controlClass}
          mode="multiple"
          allowClear
          maxTagCount="responsive"
          placeholder="Any tags"
          aria-label="Tags"
          value={filters.tagIds}
          onChange={(tagIds) => set({ tagIds })}
          optionFilterProp="label"
          // Grouped by axis, since the same label can sit on two axes.
          options={(tagCategories ?? [])
            .filter((c) => c.tags.length > 0)
            .map((c) => ({
              label: c.name,
              title: c.name,
              options: c.tags.map((t) => ({
                value: t.id,
                label: `${t.isActive ? t.name : `${t.name} (Inactive)`} (${perTag.get(t.id) ?? 0})`,
              })),
            }))}
        />
      )}
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
        <FilterControls {...props} stacked={false} />
        {active > 0 && (
          <ResetFilters
            onReset={() => props.onChange(DEFAULT_REQUEST_FILTERS)}
          />
        )}
      </Flex>
    )
  }

  return (
    <ConfigProvider theme={TOUCH_THEME}>
      <Flex gap={8} align="center" className={styles.bar}>
        <Badge count={active} size="small">
          <Button icon={<FilterOutlined />} onClick={() => setOpen(true)}>
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
              <ResetFilters
                onReset={() => props.onChange(DEFAULT_REQUEST_FILTERS)}
              />
            ) : null
          }
        >
          <Flex vertical gap={12}>
            <FilterControls {...props} stacked />
          </Flex>
        </Drawer>
      </Flex>
    </ConfigProvider>
  )
}

export default RequestFilterBar
