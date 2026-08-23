'use client'

import { PageTitle } from '@/src/components/common'
import { SprintsGrid } from '@/src/components/common/planning'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetSprintsQuery } from '@/src/store/features/planning/sprints-api'
import { FC } from 'react'

const SprintsPage: FC = () => {
  useDocumentTitle('Sprints')

  const { data: sprintsData, isLoading, refetch } = useGetSprintsQuery()

  return (
    <>
      <PageTitle title="Sprints" />
      <SprintsGrid
        sprints={sprintsData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="planning-sprints"
      />
    </>
  )
}

const SprintsPageWithAuthorization = authorizePage(
  SprintsPage,
  'Permission',
  'Permissions.Iterations.View',
)

export default SprintsPageWithAuthorization
