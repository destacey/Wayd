'use client'

import { WaydGrid, PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ScoringModelListDto } from '@/src/services/wayd-api'
import { useGetScoringModelsQuery } from '@/src/store/features/scoring/scoring-models-api'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import CreateScoringModelForm from './_components/create-scoring-model-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const ScoringModelCellRenderer = ({
  value,
  data,
}: ICellRendererParams<ScoringModelListDto>) => {
  return <Link href={`./scoring-models/${data!.key}`}>{value}</Link>
}

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

  const columnDefs = useMemo<ColDef<ScoringModelListDto>[]>(
    () => [
      { field: 'id', hide: true },
      { field: 'key', width: 90 },
      { field: 'name', cellRenderer: ScoringModelCellRenderer, sort: 'asc' },
      { field: 'state.name', headerName: 'State', width: 100 },
      { field: 'criterionCount', headerName: 'Criteria', width: 110 },
      { field: 'scaleCount', headerName: 'Scales', width: 110 },
      { field: 'outputCount', headerName: 'Outputs', width: 110 },
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
        height={600}
        columnDefs={columnDefs}
        rowData={scoringModelData}
        loadData={refresh}
        loading={isLoading}
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

