'use client'

import PageTitle from '@/src/components/common/page-title'
import { useState } from 'react'
import { Button, Dropdown, Flex, MenuProps, Tabs } from 'antd'
import { DownOutlined } from '@ant-design/icons'
import Link from 'next/link'
import type { ItemType } from 'antd/es/menu/interface'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import { PageActions } from '@/src/components/common'
import { JobStateFilter } from '@/src/services/wayd-api'
import {
  JOB_SERVER_POLLING_MS,
  STATISTICS_POLLING_MS,
  useGetJobServersQuery,
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
import JobHealthSummary from './_components/job-health-summary'

/** The scheduler's three peer views. */
type JobsView = 'jobs' | 'recurring' | 'servers'

const tabLabel = (label: string, count: number | undefined) =>
  count === undefined ? label : `${label} (${count.toLocaleString()})`

const BackgroundJobsListPage = () => {
  useDocumentTitle('Background Jobs')
  const [openCreateRecurringJobForm, setOpenCreateRecurringJobForm] =
    useState(false)
  const [view, setView] = useState<JobsView>('jobs')
  const [jobState, setJobState] = useState<JobStateFilter>(
    JobStateFilter.Processing,
  )

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
  const { data: servers } = useGetJobServersQuery(undefined, {
    pollingInterval: JOB_SERVER_POLLING_MS,
  })

  const current = statistics?.current
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

  const showJobsInState = (state: JobStateFilter) => {
    setJobState(state)
    setView('jobs')
  }

  // Every job type can be run on demand, including the ones that cannot be
  // scheduled — the recurring form filters on IsSchedulable, this menu does not.
  const runJobMenuItems: ItemType[] = (() => {
    const grouped = new Map<string, typeof jobTypeData>()
    for (const jobType of [...jobTypeData].sort(
      (a, b) =>
        caseInsensitiveCompare(a.groupName, b.groupName) || a.order - b.order,
    )) {
      const group = grouped.get(jobType.groupName) ?? []
      group.push(jobType)
      grouped.set(jobType.groupName, group)
    }

    return [...grouped].map(([groupName, jobTypes]) => ({
      key: groupName,
      type: 'group' as const,
      label: groupName,
      children: jobTypes.map((jobType) => ({
        label: jobType.name,
        key: `job-type-${jobType.id}`,
        title: jobType.description,
        onClick: () => handleRunJob(jobType.id, jobType.name),
      })),
    }))
  })()

  const actionsMenuItems: MenuProps['items'] = (() => {
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL
    const items: ItemType[] = []

    if (canRunBackgroundJobs && jobTypeData.length > 0) {
      items.push({
        label: 'Create Recurring Job',
        key: 'create-recurring-job',
        onClick: () => setOpenCreateRecurringJobForm(true),
      })
    }

    // Guarded on the base URL as well as the permission: without it the link would
    // resolve to "undefined/jobs" (same treatment as the API Specification link in profile-menu).
    if (canViewHangfire && apiBaseUrl) {
      if (items.length > 0) {
        items.push({ type: 'divider' })
      }
      items.push({
        label: (
          <Link href={`${apiBaseUrl}/jobs`} target="_blank">
            Hangfire Dashboard
          </Link>
        ),
        key: 'view-hangfire',
      })
    }

    return items
  })()

  const onCreateRecurringJobFormClosed = () => {
    setOpenCreateRecurringJobForm(false)
  }

  return (
    <div className="page-gutters">
      <PageTitle
        title="Background Jobs"
        actions={
          <Flex gap="small" align="center">
            {canRunBackgroundJobs && runJobMenuItems.length > 0 && (
              <Dropdown menu={{ items: runJobMenuItems }} trigger={['click']}>
                <Button type="primary">
                  Run a Job <DownOutlined />
                </Button>
              </Dropdown>
            )}
            <PageActions actionItems={actionsMenuItems} />
          </Flex>
        }
      />
      <JobHealthSummary
        statistics={statistics}
        servers={servers}
        isLoading={statisticsLoading}
        onSelectState={showJobsInState}
      />
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
          {
            key: 'jobs',
            label: 'Jobs',
            children: (
              <JobsTab
                state={jobState}
                onStateChange={setJobState}
                counts={current}
              />
            ),
          },
          {
            key: 'recurring',
            label: tabLabel('Recurring', current?.recurring),
            children: <RecurringJobsTab />,
          },
          {
            key: 'servers',
            label: tabLabel('Servers', servers?.length),
            children: <JobServersTab />,
          },
        ]}
      />
      {openCreateRecurringJobForm && (
        <CreateRecurringJobForm
          jobTypes={jobTypeData}
          onFormCreate={onCreateRecurringJobFormClosed}
          onFormCancel={onCreateRecurringJobFormClosed}
        />
      )}
    </div>
  )
}

const PageWithAuthorization = authorizePage(
  BackgroundJobsListPage,
  'Permission',
  'Permissions.BackgroundJobs.View',
)

export default PageWithAuthorization
