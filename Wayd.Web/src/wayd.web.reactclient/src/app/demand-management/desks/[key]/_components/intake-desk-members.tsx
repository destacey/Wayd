'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  IntakeDeskDto,
  IntakeDeskMemberDto,
  IntakeDeskRole,
} from '@/src/services/wayd-api'
import { Alert, Typography } from 'antd'
import { FC } from 'react'

const { Paragraph } = Typography

const roleName = (role: IntakeDeskRole) =>
  role === IntakeDeskRole.Admin ? 'Desk Admin' : 'Agent'

const columns: ColumnDef<IntakeDeskMemberDto, any>[] = [
  { id: 'name', accessorKey: 'employee.name', header: 'Name', size: 260 },
  {
    id: 'role',
    accessorFn: (member) => roleName(member.role),
    header: 'Role',
    size: 160,
  },
  {
    id: 'triages',
    accessorFn: (member) => (member.isTriager ? 'Yes' : ''),
    header: 'Triages',
    size: 120,
  },
]

export interface IntakeDeskMembersProps {
  desk: IntakeDeskDto
}

/**
 * Who works the desk. Says what each role means, because membership is what grants access to the desk's
 * requests — no permission is needed beside it.
 */
const IntakeDeskMembers: FC<IntakeDeskMembersProps> = ({ desk }) => (
  <>
    <Paragraph type="secondary">
      Agents act on this desk&apos;s requests. Desk Admins also run the desk and
      are the only ones who can delete its requests. Being named here is all it
      takes, and working this desk gives nothing on any other. Triagers, of any
      role, are told about requests waiting here unassigned.
    </Paragraph>
    {desk.members.length > 0 && !desk.members.some((m) => m.isTriager) && (
      <Alert
        type="warning"
        showIcon
        title="Nobody triages this desk, so nobody is told about its new requests."
        style={{ marginBottom: 12 }}
      />
    )}
    <WaydGrid
      variant="simple"
      columns={columns}
      data={desk.members}
      emptyMessage="Nobody works this desk"
    />
  </>
)

export default IntakeDeskMembers
