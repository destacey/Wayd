'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ExpenditureCategoryListDto } from '@/src/services/wayd-api'
import { useGetExpenditureCategoriesQuery } from '@/src/store/features/ppm/expenditure-categories-api'
import type { ColumnDef } from '@tanstack/react-table'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import { CreateExpenditureCategoryForm } from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const ExpenditureCategoriesPage = () => {
  useDocumentTitle('PPM - Expenditure Categories')
  const [
    openCreateExpenditureCategoryForm,
    setOpenCreateExpenditureCategoryForm,
  ] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: categoryData,
    isLoading,
    error,
    refetch,
  } = useGetExpenditureCategoriesQuery()

  const { hasPermissionClaim } = useAuth()
  const canCreateExpenditureCategory = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Create',
  )
  const showActions = canCreateExpenditureCategory

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading expenditure categories',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<ExpenditureCategoryListDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`./expenditure-categories/${row.original.id}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 100,
        meta: { filterType: 'set' },
      },
      {
        id: 'isCapitalizable',
        accessorKey: 'isCapitalizable',
        header: 'Capitalizable',
        size: 120,
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'requiresDepreciation',
        accessorKey: 'requiresDepreciation',
        header: 'Requires Depreciation',
        size: 170,
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'accountingCode',
        accessorKey: 'accountingCode',
        header: 'Accounting Code',
        size: 150,
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const actions = !showActions ? null : (
    <>
      {canCreateExpenditureCategory && (
        <Button onClick={() => setOpenCreateExpenditureCategoryForm(true)}>
          Create Expenditure Category
        </Button>
      )}
    </>
  )

  const onCreateExpenditureCategoryFormClosed = (wasCreated: boolean) => {
    setOpenCreateExpenditureCategoryForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Expenditure Categories" actions={actions} />

      <WaydGrid
        columns={columns}
        data={categoryData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="expenditure-categories"
      />
      {openCreateExpenditureCategoryForm && (
        <CreateExpenditureCategoryForm
          onFormComplete={() => onCreateExpenditureCategoryFormClosed(true)}
          onFormCancel={() => onCreateExpenditureCategoryFormClosed(false)}
        />
      )}
    </>
  )
}

const ExpenditureCategoriesPageWithAuthorization = authorizePage(
  ExpenditureCategoriesPage,
  'Permission',
  'Permissions.ExpenditureCategories.View',
)

export default ExpenditureCategoriesPageWithAuthorization
