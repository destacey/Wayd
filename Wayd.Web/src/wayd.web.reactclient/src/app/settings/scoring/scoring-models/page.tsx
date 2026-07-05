'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ScoringModelListDto } from '@/src/services/wayd-api'
import { useGetScoringModelsQuery } from '@/src/store/features/scoring/scoring-models-api'
import type { ColumnDef } from '@tanstack/react-table'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import CreateScoringModelForm from './_components/create-scoring-model-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const ScoringModelsPage = () => {
  useDocumentTitle('Settings - Scoring Models')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: scoringModelData,
    isLoading,
    error,
    refetch,
  } = useGetScoringModelsQuery(null)

  const { hasPermissionClaim } = useAuth()
  const canCreateScoringModel = hasPermissionClaim(
    'Permissions.ScoringModels.Create',
  )
  const showActions = canCreateScoringModel

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading scoring models',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<ScoringModelListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`./scoring-models/${row.original.key}`}>
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
        id: 'criterionCount',
        accessorKey: 'criterionCount',
        header: 'Criteria',
        size: 110,
      },
      {
        id: 'scaleCount',
        accessorKey: 'scaleCount',
        header: 'Scales',
        size: 110,
      },
      {
        id: 'outputCount',
        accessorKey: 'outputCount',
        header: 'Outputs',
        size: 110,
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const actions = !showActions ? null : (
    <>
      {canCreateScoringModel && (
        <Button onClick={() => setOpenCreateForm(true)}>
          Create Scoring Model
        </Button>
      )}
    </>
  )

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Scoring Models" actions={actions} />

      <WaydGrid
        columns={columns}
        data={scoringModelData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-scoring-models"
        csvFileName="scoring-models"
      />
      {openCreateForm && (
        <CreateScoringModelForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </>
  )
}

const ScoringModelsPageWithAuthorization = authorizePage(
  ScoringModelsPage,
  'Permission',
  'Permissions.ScoringModels.View',
)

export default ScoringModelsPageWithAuthorization
