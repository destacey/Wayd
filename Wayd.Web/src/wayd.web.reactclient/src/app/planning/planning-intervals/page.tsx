'use client'

import {
  WaydGrid,
  renderPlanningIntervalLink,
} from '@/src/components/common/wayd-grid'
import PageTitle from '@/src/components/common/page-title'
import { useState, useMemo } from 'react'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import dayjs from 'dayjs'
import { CreatePlanningIntervalForm } from './_components'
import useAuth from '../../../components/contexts/auth'
import { Button } from 'antd'
import { authorizePage } from '../../../components/hoc'
import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'

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

  const columns = useMemo<ColumnDef<PlanningIntervalListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderPlanningIntervalLink(row.original),
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        meta: { columnType: 'dateOnly' },
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
        columns={columns}
        data={data}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="planning-intervals"
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
