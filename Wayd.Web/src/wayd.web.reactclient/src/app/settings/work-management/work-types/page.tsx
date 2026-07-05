'use client'

import { PageTitle } from '@/src/components/common'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useAppDispatch, useAppSelector, useDocumentTitle } from '@/src/hooks'
import { WorkTypeDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { useEffect, useMemo, useState } from 'react'
import { setIncludeInactive } from '../../../../store/features/work-management/work-type-slice'
import { authorizePage } from '@/src/components/hoc'
import { useGetWorkTypesQuery } from '@/src/store/features/work-management/work-type-api'
import useAuth from '@/src/components/contexts/auth'
import EditWorkTypeForm from './_components/edit-work-type-form'
import Link from 'next/link'
import { ItemType } from 'antd/es/menu/interface'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '@/src/components/common/control-items-menu'

const WorkTypesPage = () => {
  useDocumentTitle('Work Management - Work Types')
  const [openUpdateWorkTypeForm, setOpenUpdateWorkTypeForm] =
    useState<boolean>(false)
  const [editWorkTypeId, setEditWorkTypeId] = useState<number | undefined>(
    undefined,
  )

  const { includeInactive } = useAppSelector((state) => state.workType)

  const {
    data: workTypes,
    isLoading,
    error,
    refetch,
  } = useGetWorkTypesQuery(includeInactive)
  const dispatch = useAppDispatch()

  const { hasClaim } = useAuth()
  const canUpdateWorkTypes = hasClaim(
    'Permission',
    'Permissions.WorkTypes.Update',
  )
  const canViewWorkTypeHierarchy = hasClaim(
    'Permission',
    'Permissions.WorkTypeLevels.View',
  )

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const columns = useMemo<ColumnDef<WorkTypeDto, any>[]>(() => {
    const editWorkTypeButtonClicked = (id: number) => {
      setEditWorkTypeId(id)
      setOpenUpdateWorkTypeForm(true)
    }

    return [
      createActionsColumn<WorkTypeDto>({
        hide: !canUpdateWorkTypes,
        ariaLabel: 'Work type actions',
        getItems: (workType): ItemType[] =>
          canUpdateWorkTypes
            ? [
                {
                  key: 'edit',
                  label: 'Edit',
                  onClick: () => editWorkTypeButtonClicked(workType.id),
                },
              ]
            : [],
      }),
      { id: 'name', accessorKey: 'name', header: 'Name' },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
      {
        id: 'level',
        accessorKey: 'level.name',
        header: 'Level',
        meta: { filterType: 'set' },
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        meta: { columnType: 'yesNo' },
      },
    ]
  }, [canUpdateWorkTypes])

  const refresh = async () => {
    refetch()
  }

  const actions = () => {
    return (
      <>
        {canViewWorkTypeHierarchy && (
          <Link href="/settings/work-management/work-types/hierarchy">
            Work Type Hierarchy
          </Link>
        )}
      </>
    )
  }

  const onIncludeInactiveChange = (checked: boolean) => {
    dispatch(setIncludeInactive(checked))
    refresh()
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Inactive"
          checked={includeInactive}
          onChange={onIncludeInactiveChange}
        />
      ),
      key: 'include-inactive',
      onClick: () => onIncludeInactiveChange(!includeInactive),
    },
  ]

  const onEditWorkTypeFormClosed = (wasSaved: boolean) => {
    setOpenUpdateWorkTypeForm(false)
    setEditWorkTypeId(undefined)
    if (wasSaved) {
      refresh()
    }
  }

  return (
    <>
      <PageTitle title="Work Types" actions={actions()} />

      <WaydGrid
        columns={columns}
        data={workTypes ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-work-types"
        csvFileName="work-types"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
      {openUpdateWorkTypeForm && (
        <EditWorkTypeForm
          workTypeId={editWorkTypeId!}
          onFormSave={() => onEditWorkTypeFormClosed(true)}
          onFormCancel={() => onEditWorkTypeFormClosed(false)}
        />
      )}
    </>
  )
}

const WorkTypesPageWithAuthorization = authorizePage(
  WorkTypesPage,
  'Permission',
  'Permissions.WorkTypes.View',
)

export default WorkTypesPageWithAuthorization
