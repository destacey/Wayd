'use client'

import { PageTitle } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import {
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeDesksQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Button, Flex } from 'antd'
import { FC, useEffect, useState } from 'react'
import CreateIntakeDeskForm from '../_components/create-intake-desk-form'
import {
  UnownedDeskTag,
  deskHref,
  describeWaiting,
} from '../_components/desk-status'

/**
 * Every desk, with who works it and, on the desks the reader works, what is waiting there. A desk with no
 * members is flagged, because that is where requests go to be forgotten. Creating a desk is the Demand
 * Management Admin's; each desk is run from its own page.
 */
const IntakeDesksPage: FC = () => {
  useDocumentTitle('Desks')
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const canCreateDesk = hasPermissionClaim(
    'Permissions.IntakeRequests.Administer',
  )
  const [openCreateDesk, setOpenCreateDesk] = useState(false)

  const { data, isLoading, error, refetch } = useGetIntakeDesksQuery()
  const { data: visibilities } = useGetIntakeDeskVisibilitiesQuery()

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load desks.')
    }
  }, [error, messageApi])

  const visibilityName = (code: string) =>
    visibilities?.find((v) => v.code === code)?.name ?? code

  const columns: ColumnDef<IntakeDeskDto, any>[] = [
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 260,
      cell: ({ row }) => (
        <Flex gap={8} align="center">
          <EntityLink href={deskHref(row.original.key)}>
            {row.original.name}
          </EntityLink>
          <UnownedDeskTag desk={row.original} />
        </Flex>
      ),
    },
    {
      id: 'visibility',
      accessorFn: (desk) => visibilityName(desk.visibility),
      header: 'Visibility',
      size: 140,
      meta: { filterType: 'set' },
    },
    {
      id: 'members',
      accessorFn: (desk) => desk.members.length,
      header: 'Members',
      size: 110,
    },
    {
      id: 'waiting',
      accessorFn: (desk) => (desk.waiting ? describeWaiting(desk.waiting) : ''),
      header: 'Waiting',
      size: 280,
    },
    {
      id: 'isOpen',
      accessorFn: (desk) => (desk.isOpen ? 'Yes' : 'No'),
      header: 'Open to New Requests',
      size: 180,
      meta: { filterType: 'set' },
    },
    {
      id: 'isRequesterSelectable',
      accessorFn: (desk) => (desk.isRequesterSelectable ? 'Yes' : 'No'),
      header: 'Requesters Can Choose',
      size: 190,
      meta: { filterType: 'set', hiddenByDefault: true },
    },
  ]

  return (
    <div className="page-gutters">
      <PageTitle
        title="Desks"
        tooltip="The review responsibilities requests are submitted to. A desk with no members has nobody looking at what arrives."
        actions={
          canCreateDesk ? (
            <Button onClick={() => setOpenCreateDesk(true)}>Create Desk</Button>
          ) : null
        }
      />
      <WaydGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        onRefresh={refetch}
        persistStateKey="demand-management-desks"
        csvFileName="desks"
        emptyMessage="No desks found"
        initialSorting={[{ id: 'name', desc: false }]}
      />
      {openCreateDesk && (
        <CreateIntakeDeskForm
          onFormComplete={() => setOpenCreateDesk(false)}
          onFormCancel={() => setOpenCreateDesk(false)}
        />
      )}
    </div>
  )
}

// No permission gate: a desk's existence, description and members are not hidden, only the requests on a
// private one, and the server sends waiting counts only to those who may see them.
const IntakeDesksPageWithAuthorization = requireFeatureFlag(
  IntakeDesksPage,
  'demand-management',
)

export default IntakeDesksPageWithAuthorization
