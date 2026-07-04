import { useMemo } from 'react'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@tanstack/react-table'
import useAuth, { Claim } from '../../../components/contexts/auth'

const ClaimsGrid = () => {
  const { user } = useAuth()

  const columns = useMemo<ColumnDef<Claim, any>[]>(
    () => [
      { id: 'type', accessorKey: 'type', header: 'Type', size: 400 },
      { id: 'value', accessorKey: 'value', header: 'Value', size: 400 },
    ],
    [],
  )

  return (
    <WaydGrid
      columns={columns}
      data={user?.claims ?? []}
      csvFileName="claims"
    />
  )
}

export default ClaimsGrid
