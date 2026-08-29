'use client'

import PageTitle from '@/src/components/common/page-title'
import MetricCard from '@/src/components/common/metrics/metric-card'
import { useState } from 'react'
import { Flex, MenuProps, Tabs, Typography } from 'antd'
import Link from 'next/link'
import { ItemType } from 'antd/es/menu/interface'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import { PageActions } from '@/src/components/common'
import {
  STATISTICS_POLLING_MS,
  useGetJobStatisticsQuery,
  useGetJobTypesQuery,
  useRunJobMutation,
} from '@/src/store/features/admin/background-jobs-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import CreateRecurringJobForm from './create-recurring-job-form'
import JobsTab from './_components/jobs-tab'
import RecurringJobsTab from './_components/recurring-jobs-tab'
import JobServersTab from './_components/job-servers-tab'

/** The scheduler's three peer views. */
type JobsView = 'jobs' | 'recurring' | 'servers'

const BackgroundJobsListPage = () => {
  useDocumentTitle('Background Jobs')
  const [openCreateRecurringJobForm, setOpenCreateRecurringJobForm] =
    useState(false)
  const [view, setView] = useState<JobsView>('jobs')

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  // The run and create-recurring endpoints both require Run; gating on Create
  // showed the menu to users the API would reject.
  const canRunBackgroundJobs = hasPermissionClaim(
    'Permissions.BackgroundJobs.Run',
  )
  // Kept until the Hangfire dashboard is retired, so this UI can be cross-checked
  // against it. Removed together with the dashboard mount.
  const canViewHangfire = hasPermissionClaim('Permissions.Hangfire.View')

  const { data: statistics, isLoading: statisticsLoading } =
    useGetJobStatisticsQuery(undefined, {
      pollingInterval: STATISTICS_POLLING_MS,
    })

  const current = statistics?.current
  const allTime = statistics?.allTime
  const { data: jobTypeData = [] } = useGetJobTypesQuery()
  const [runJob] = useRunJobMutation()

  const handleRunJob = async (jobTypeId: number, jobTypeName: string) => {
    try {
      await runJob(jobTypeId).unwrap()
      messageApi.success(`${jobTypeName} queued.`)
    } catch {
      messageApi.error(`Failed to queue ${jobTypeName}.`)
    }
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL
    const items: ItemType[] = []

    // Guarded on the base URL as well as the permission: without it the link would
    // resolve to "undefined/jobs" (same treatment as the Swagger link in profile-menu).
    if (canViewHangfire && apiBaseUrl) {
      items.push({
        label: (
          <Link href={`${apiBaseUrl}/jobs`} target="_blank">
            Hangfire Dashboard
          </Link>
        ),
        key: 'view-hangfire',
      })
    }

    if (!canRunBackgroundJobs || jobTypeData.length === 0) {
      return items
    }

    if (items.length > 0) {
      items.push({ type: 'divider' })
    }

    items.push({
      label: 'Create Recurring Job',
      key: 'create-recurring-job',
      onClick: () => setOpenCreateRecurringJobForm(true),
    })
    items.push({ type: 'divider' })

    // Every job type can be run on demand, including the ones that cannot be
    // scheduled — the recurring form filters on IsSchedulable, this menu does not.
    const grouped = new Map<string, typeof jobTypeData>()
    for (const jobType of [...jobTypeData].sort(
      (a, b) =>
        caseInsensitiveCompare(a.groupName, b.groupName) || a.order - b.order,
    )) {
      const group = grouped.get(jobType.groupName) ?? []
      group.push(jobType)
      grouped.set(jobType.groupName, group)
    }

    for (const [groupName, jobTypes] of grouped) {
      items.push({
        key: groupName,
        type: 'group',
        label: groupName,
        children: jobTypes.map((jobType) => ({
          label: jobType.name,
          key: `job-type-${jobType.id}`,
          onClick: () => handleRunJob(jobType.id, jobType.name),
        })),
      })
    }

    return items
  })()

  const onCreateRecurringJobFormClosed = () => {
    setOpenCreateRecurringJobForm(false)
  }

  return (
    <>
      <PageTitle
        title="Background Jobs"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Typography.Text type="secondary" style={{ display: 'block' }}>
        Current
      </Typography.Text>
      <Flex gap={12} wrap style={{ marginTop: 8, marginBottom: 16 }}>
        <MetricCard
          title="Processing"
          value={current?.processing ?? 0}
          loading={statisticsLoading}
          tooltip="Jobs a worker is executing right now."
        />
        <MetricCard
          title="Enqueued"
          value={current?.enqueued ?? 0}
          loading={statisticsLoading}
          tooltip="Jobs waiting for a free worker."
        />
        <MetricCard
          title="Scheduled"
          value={current?.scheduled ?? 0}
          loading={statisticsLoading}
          tooltip="Jobs queued to run at a future time, including failed jobs waiting out a retry delay."
        />
        {current?.retries != null && (
          <MetricCard
            title="Retries"
            value={current.retries}
            loading={statisticsLoading}
            tooltip="Jobs waiting out a retry cooldown after a failed attempt. These also appear under Scheduled."
          />
        )}
        {current?.awaiting != null && (
          <MetricCard
            title="Awaiting"
            value={current.awaiting}
            loading={statisticsLoading}
            tooltip="Continuations held until the job they depend on finishes."
          />
        )}
        <MetricCard
          title="Failed"
          value={current?.failed ?? 0}
          loading={statisticsLoading}
          valueStyle={
            current?.failed ? { color: 'var(--ant-color-error)' } : undefined
          }
          tooltip="Jobs that exhausted every retry. These persist until requeued or deleted."
        />
        <MetricCard
          title="Succeeded"
          value={current?.succeeded ?? 0}
          loading={statisticsLoading}
          tooltip="Succeeded jobs still retained. They are purged after a short window, so this stays low even on a busy system — see All time for the running total."
        />
        <MetricCard
          title="Recurring"
          value={current?.recurring ?? 0}
          loading={statisticsLoading}
          tooltip="Registered cron schedules. Manage them on the Recurring tab."
        />
        <MetricCard
          title="Servers"
          value={current?.servers ?? 0}
          loading={statisticsLoading}
          tooltip="Worker processes polling for jobs. Zero means nothing will run."
        />
      </Flex>
      <Typography.Text type="secondary" style={{ display: 'block' }}>
        All time
      </Typography.Text>
      <Flex gap={12} wrap style={{ marginTop: 8, marginBottom: 16 }}>
        <MetricCard
          title="Succeeded"
          value={allTime?.succeeded ?? 0}
          loading={statisticsLoading}
          tooltip="Every job that has ever completed successfully. A running total kept by the scheduler's counters — it keeps climbing after the job records themselves are purged."
        />
        <MetricCard
          title="Deleted"
          value={allTime?.deleted ?? 0}
          loading={statisticsLoading}
          tooltip="Every job that has ever been deleted, whether manually or by exhausting its retries."
        />
      </Flex>
      {/*
        Tabs, not a record's section rail: these are three peer views of the
        scheduler rather than sections of an entity, and this page has no
        record for a rail to belong to. The record pattern drops tabs; an
        operational page is not a record.
      */}
      <Tabs
        activeKey={view}
        onChange={(next) => setView(next as JobsView)}
        items={[
          { key: 'jobs', label: 'Jobs', children: <JobsTab /> },
          {
            key: 'recurring',
            label: 'Recurring',
            children: <RecurringJobsTab />,
          },
          { key: 'servers', label: 'Servers', children: <JobServersTab /> },
        ]}
      />
      {openCreateRecurringJobForm && (
        <CreateRecurringJobForm
          jobTypes={jobTypeData}
          onFormCreate={onCreateRecurringJobFormClosed}
          onFormCancel={onCreateRecurringJobFormClosed}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  BackgroundJobsListPage,
  'Permission',
  'Permissions.BackgroundJobs.View',
)

export default PageWithAuthorization
