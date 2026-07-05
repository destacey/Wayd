'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetProgramsQuery } from '@/src/store/features/ppm/programs-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateProgramForm } from './_components'
import { ProgramsFilterBar, ProgramsGrid } from '../_components'
import { useMessage } from '@/src/components/contexts/messaging'

// Program status enum values matching the backend
const PROGRAM_STATUS = {
  Proposed: 1,
  Active: 2,
  Completed: 3,
  Cancelled: 4,
} as const

const DEFAULT_STATUSES = [PROGRAM_STATUS.Active]

const ProgramsPage: FC = () => {
  useDocumentTitle('Programs')
  const [openCreateProgramForm, setOpenCreateProgramForm] =
    useState<boolean>(false)
  const [selectedStatuses, setSelectedStatuses] =
    useState<number[]>(DEFAULT_STATUSES)
  const [selectedPortfolioId, setSelectedPortfolioId] = useState<
    string | null
  >(null)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateProgram = hasPermissionClaim('Permissions.Programs.Create')
  const showActions = canCreateProgram

  const {
    data: programData,
    isLoading,
    error,
    refetch,
  } = useGetProgramsQuery({
    status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
    portfolioId: selectedPortfolioId ?? undefined,
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load programs.')
    }
  }, [error, messageApi])

  const handleStatusChange = (statuses: number[]) => {
    setSelectedStatuses(statuses)
  }

  const actions = !showActions ? null : (
    <>
      {canCreateProgram && (
        <Button onClick={() => setOpenCreateProgramForm(true)}>
          Create Program
        </Button>
      )}
    </>
  )

  const onCreateProgramFormClosed = (wasCreated: boolean) => {
    setOpenCreateProgramForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Programs" actions={actions} />
      <ProgramsFilterBar
        selectedStatuses={selectedStatuses}
        onStatusChange={handleStatusChange}
        selectedPortfolioId={selectedPortfolioId}
        onPortfolioChange={setSelectedPortfolioId}
      />
      <ProgramsGrid
        programs={programData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="ppm-programs"
      />
      {openCreateProgramForm && (
        <CreateProgramForm
          onFormComplete={() => onCreateProgramFormClosed(true)}
          onFormCancel={() => onCreateProgramFormClosed(false)}
        />
      )}
    </>
  )
}

const ProgramsPageWithAuthorization = authorizePage(
  ProgramsPage,
  'Permission',
  'Permissions.Programs.View',
)

export default ProgramsPageWithAuthorization
