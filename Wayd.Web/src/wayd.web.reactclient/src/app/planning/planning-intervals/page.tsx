'use client'

import WaydGrid from '@/src/components/common/wayd-grid'
import PageTitle from '@/src/components/common/page-title'
import Link from 'next/link'
import { useState, useMemo } from 'react'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import dayjs from 'dayjs'
import { CreatePlanningIntervalForm } from './_components'
import useAuth from '../../../components/contexts/auth'
import { Button } from 'antd'
import { authorizePage } from '../../../components/hoc'
import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalListDto } from '@/src/services/wayd-api'
import { ColDef, ICellRendererParams } from 'ag-grid-community'

const PlanningIntervalLinkCellRenderer = ({
  value,
  data,
}: ICellRendererParams<PlanningIntervalListDto>) => {
  return <Link href={`/planning/planning-intervals/${data!.key}`}>{value}</Link>
}

const stateOrder = ['Active', 'Future', 'Completed']

const PlanningIntervalListPage = () => {
  useDocumentTitle('Planning Intervals')

  const [openCreatePlanningIntervalForm, setOpenCreatePlanningIntervalForm] =
    useState<boolean>(false)

  const { data: piData, isLoading, refetch } = useGetPlanningIntervalsQuery()

  const { hasPermissionClaim } = useAuth()
  const canCreatePlanningInterval = hasPermissionClaim(
    'Permissions.PlanningIntervals.Create',
  )
  const showActions = canCreatePlanningInterval

  const data = !piData
    ? []
    : piData.slice().sort((a, b) => {
        const aStateIndex = stateOrder.indexOf(a.state.name)
        const bStateIndex = stateOrder.indexOf(b.state.name)
        if (aStateIndex !== bStateIndex) {
          return aStateIndex - bStateIndex
        } else {
          return dayjs(b.start).unix() - dayjs(a.start).unix()
        }
      })

  const columnDefs = useMemo<ColDef<PlanningIntervalListDto>[]>(
    () => [
      { field: 'key', width: 90 },
      { field: 'name', cellRenderer: PlanningIntervalLinkCellRenderer },
      {
        field: 'state.name',
        width: 125,
      },
      {
        field: 'start',
        type: 'dateOnly',
      },
      {
        field: 'end',
        type: 'dateOnly',
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const onCreatePlanningIntervalFormClosed = (wasCreated: boolean) => {
    setOpenCreatePlanningIntervalForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  const actions = () => {
    return (
      <>
        {canCreatePlanningInterval && (
          <Button onClick={() => setOpenCreatePlanningIntervalForm(true)}>
            Create Planning Interval
          </Button>
        )}
      </>
    )
  }

  return (
    <>
      <br />
      <PageTitle
        title="Planning Intervals"
        actions={showActions && actions()}
      />
      <WaydGrid
        columnDefs={columnDefs}
        rowData={data}
        loading={isLoading}
        loadData={refresh}
      />
      {openCreatePlanningIntervalForm && (
        <CreatePlanningIntervalForm
          onFormCreate={() => onCreatePlanningIntervalFormClosed(true)}
          onFormCancel={() => onCreatePlanningIntervalFormClosed(false)}
        />
      )}
    </>
  )
}

const PlanningIntervalListPageWithAuthorization = authorizePage(
  PlanningIntervalListPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalListPageWithAuthorization
