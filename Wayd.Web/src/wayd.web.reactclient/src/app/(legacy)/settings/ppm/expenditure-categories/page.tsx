'use client'

import { PageTitle } from '@/src/components/common'
import {
  ConfigListPanel,
  useSelectedRecord,
} from '@/src/components/common/config-list'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ExpenditureCategoryListDto } from '@/src/services/wayd-api'
import {
  useGetExpenditureCategoriesQuery,
  useGetExpenditureCategoryQuery,
} from '@/src/store/features/ppm/expenditure-categories-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Button } from 'antd'
import { Suspense, useEffect, useMemo, useState } from 'react'
import {
  CreateExpenditureCategoryForm,
  ExpenditureCategoryPanel,
  useExpenditureCategoryActions,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const ExpenditureCategoriesPage = () => {
  useDocumentTitle('PPM - Expenditure Categories')
  const [
    openCreateExpenditureCategoryForm,
    setOpenCreateExpenditureCategoryForm,
  ] = useState<boolean>(false)

  const messageApi = useMessage()
  const { selectedId, select, clear } = useSelectedRecord()

  const {
    data: categoryData,
    isLoading,
    error,
    refetch,
  } = useGetExpenditureCategoriesQuery()

  // The list carries every field but the description, so the panel still asks
  // for the record. Cached per id, so reopening a row costs nothing.
  const {
    data: selectedCategory,
    isLoading: isLoadingSelected,
    error: selectedError,
    refetch: refetchSelected,
  } = useGetExpenditureCategoryQuery(Number(selectedId), {
    skip: !selectedId,
  })

  const { hasPermissionClaim } = useAuth()
  const canCreateExpenditureCategory = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Create',
  )

  const { actions: recordActions, dialogs } = useExpenditureCategoryActions({
    expenditureCategory: selectedCategory,
    onChanged: () => {
      refetch()
      refetchSelected()
    },
    onDeleted: () => {
      clear()
      refetch()
    },
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading expenditure categories',
      )
      console.error(error)
    }
  }, [error, messageApi])

  useEffect(() => {
    if (selectedError) {
      messageApi.error(
        (isApiError(selectedError) ? selectedError.detail : undefined) ??
          'An error occurred while loading expenditure category details',
      )
      console.error(selectedError)
    }
  }, [selectedError, messageApi])

  const columns = useMemo<ColumnDef<ExpenditureCategoryListDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
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

  const actions = canCreateExpenditureCategory ? (
    <Button onClick={() => setOpenCreateExpenditureCategoryForm(true)}>
      Create Expenditure Category
    </Button>
  ) : null

  const onCreateExpenditureCategoryFormClosed = (wasCreated: boolean) => {
    setOpenCreateExpenditureCategoryForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Expenditure Categories" actions={actions} />

      <ConfigListPanel
        open={!!selectedId}
        onClose={clear}
        title={selectedCategory?.name}
        details={
          <ExpenditureCategoryPanel expenditureCategory={selectedCategory} />
        }
        actions={recordActions}
        isLoading={isLoadingSelected}
      >
        <WaydGrid
          columns={columns}
          data={categoryData ?? []}
          onRefresh={refresh}
          isLoading={isLoading}
          persistStateKey="settings-expenditure-categories"
          csvFileName="expenditure-categories"
          onRowActivate={(category) => select(String(category.id))}
          activatedRowId={selectedId}
          getRowActivateLabel={(category) => category.name}
        />
      </ConfigListPanel>

      {dialogs}

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

/**
 * `useSelectedRecord` reads the query string, so the page needs a Suspense
 * boundary — Next requires one around any `useSearchParams` consumer or the
 * whole route opts out of static rendering.
 */
const ExpenditureCategoriesPageWithSuspense = () => (
  <Suspense>
    <ExpenditureCategoriesPageWithAuthorization />
  </Suspense>
)

export default ExpenditureCategoriesPageWithSuspense
