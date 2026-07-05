'use client'

import PageTitle from '@/src/components/common/page-title'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import { useMemo, useState } from 'react'
import { BackgroundJobDto } from '@/src/services/wayd-api'
import { getBackgroundJobsClient } from '@/src/services/clients'
import { authorizePage } from '../../../components/hoc'
import useAuth from '../../../components/contexts/auth'
import { MenuProps } from 'antd'
import Link from 'next/link'
import { ItemType } from 'antd/es/menu/interface'
import { useDocumentTitle } from '../../../hooks'
import { PageActions } from '../../../components/common'
import { useGetJobTypesQuery } from '@/src/store/features/admin/background-jobs-api'
import CreateRecurringJobForm from './create-recurring-job-form'
import type { ColumnDef } from '@tanstack/react-table'

const BackgroundJobsListPage = () => {
  useDocumentTitle('Background Jobs')
  const [backgroundJobs, setBackgroundJobs] = useState<BackgroundJobDto[]>([])
  const [openCreateRecurringJobForm, setOpenCreateRecurringJobForm] =
    useState(false)

  const { hasClaim } = useAuth()
  const canViewHangfire = hasClaim('Permission', 'Permissions.Hangfire.View')
  const canRunBackgroundJobs = hasClaim(
    'Permission',
    'Permissions.BackgroundJobs.Create',
  )

  const columns = useMemo<ColumnDef<BackgroundJobDto, any>[]>(
    () => [
      { id: 'id', accessorKey: 'id', header: 'Id' },
      { id: 'action', accessorKey: 'action', header: 'Action' },
      {
        id: 'status',
        accessorKey: 'status',
        header: 'Status',
        meta: { filterType: 'set' },
      },
      {
        id: 'type',
        accessorKey: 'type',
        header: 'Type',
        meta: { filterType: 'set' },
      },
      { id: 'namespace', accessorKey: 'namespace', header: 'Namespace' },
      { id: 'startedAt', accessorKey: 'startedAt', header: 'Start (UTC)' },
    ],
    [],
  )

  const { data: jobTypeData = [] } = useGetJobTypesQuery()

  const getRunningJobs = async () => {
    const backgroundJobsClient = await getBackgroundJobsClient()
    const jobDtos = await backgroundJobsClient.getRunningJobs()
    setBackgroundJobs(jobDtos)
  }

  const runJob = async (jobTypeId: number) => {
    const backgroundJobsClient = await getBackgroundJobsClient()
    await backgroundJobsClient.run(jobTypeId)
    getRunningJobs()
  }
  const actionsMenuItems: MenuProps['items'] = (() => {
    const hangfireUrl = process.env.NEXT_PUBLIC_API_BASE_URL + '/jobs'
    const items: ItemType[] = []

    if (canViewHangfire) {
      items.push({
        label: (
          <Link href={hangfireUrl} target="_blank">
            Hangfire Dashboard
          </Link>
        ),
        key: 'view-hangfire',
      })
    }
    if (canRunBackgroundJobs && jobTypeData.length > 0) {
      if (canViewHangfire) {
        items.push({
          type: 'divider',
        })
      }
      items.push({
        label: 'Create Recurring Job',
        key: 'create-recurring-job',
        onClick: () => setOpenCreateRecurringJobForm(true),
      })
      items.push({
        type: 'divider',
      })

      // Sort jobTypeData by groupName and then by order
      const sortedJobTypeData = [...jobTypeData].sort((a, b) => {
        if (a.groupName === b.groupName) {
          return a.order - b.order
        }
        return a.groupName.localeCompare(b.groupName)
      })

      let currentGroupName = ''
      let currentGroupItems: ItemType[] = []

      sortedJobTypeData.forEach((jobType) => {
        if (jobType.groupName !== currentGroupName) {
          if (currentGroupName !== '') {
            items.push({
              key: currentGroupName,
              type: 'group',
              label: currentGroupName,
              children: currentGroupItems,
            })
            items.push({
              type: 'divider',
            })
          }
          currentGroupName = jobType.groupName
          currentGroupItems = []
        }
        currentGroupItems.push({
          label: jobType.name,
          key: jobType.name,
          onClick: () => runJob(jobType.id),
        })
      })

      // Add the last group
      if (currentGroupName !== '') {
        items.push({
          key: currentGroupName,
          type: 'group',
          label: currentGroupName,
          children: currentGroupItems,
        })
      }
    }
    return items
  })()

  const onCreateRecurringJobFormClosed = (wasSaved: boolean) => {
    setOpenCreateRecurringJobForm(false)
    if (wasSaved) {
      getRunningJobs()
    }
  }

  return (
    <>
      <PageTitle
        title="Background Jobs"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />

      <WaydGrid
        columns={columns}
        data={backgroundJobs}
        onRefresh={getRunningJobs}
        persistStateKey="settings-background-jobs"
        csvFileName="background-jobs"
      />
      {openCreateRecurringJobForm && (
        <CreateRecurringJobForm
          jobTypes={jobTypeData}
          onFormCreate={() => onCreateRecurringJobFormClosed(true)}
          onFormCancel={() => onCreateRecurringJobFormClosed(false)}
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
