'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Card, Typography } from 'antd'
import Link from 'next/link'
import { TEAMS_GRAPH_APPLICATION_PERMISSIONS } from './teams-permissions'

const { Paragraph, Title } = Typography

type TeamsPermissionRow = (typeof TEAMS_GRAPH_APPLICATION_PERMISSIONS)[number]

const permissionColumns: ColumnDef<TeamsPermissionRow, any>[] = [
  {
    id: 'permission',
    accessorKey: 'permission',
    header: 'Permission',
    size: 200,
    cell: ({ getValue }) => <strong>{getValue<string>()}</strong>,
  },
  {
    id: 'reason',
    accessorKey: 'reason',
    header: 'Why Wayd needs it',
  },
]

/**
 * Setup steps for the Teams communication connector. Kept in one component so the connection
 * create/edit form and detail page stay aligned with docs/reference/integrations/teams.mdx.
 */
export const TeamsSetupGuide = () => (
  <div>
    <Paragraph type="secondary">
      Register a dedicated Entra application for Wayd Teams communication. It is separate from
      sign-in and from the Entra people-sync app registration.
    </Paragraph>
    <Title level={5}>Setup</Title>
    <ol>
      <li>
        In the Entra admin portal, <strong>register a new application</strong> for Wayd Teams
        communication (not the people-sync or sign-in apps). Note the <strong>Tenant ID</strong>{' '}
        and <strong>Application (client) ID</strong>.
      </li>
      <li>
        Under <strong>Certificates &amp; secrets</strong>, create a <strong>client secret</strong>.
        Copy the value immediately — Entra only shows it once.
      </li>
      <li>
        Grant the <strong>Application permissions</strong> listed below on Microsoft Graph, then
        select <strong>Grant admin consent</strong> for the tenant.
      </li>
      <li>
        In Wayd, open <strong>Settings → System → Connections</strong>, choose{' '}
        <strong>Microsoft Teams</strong>, and enter the tenant ID, client ID, and secret.
      </li>
      <li>
        Choose <strong>Verify</strong> on the create form. Wayd runs the same hello-world Graph check
        as <strong>Test connection</strong> on the detail page. After verification succeeds, save the
        connection.
      </li>
    </ol>
    <Card
      title="Application permissions"
      size="small"
      style={{ marginTop: 8 }}
      styles={{ body: { paddingTop: 12 } }}
    >
      <Paragraph style={{ marginBottom: 12 }}>
        Application permissions on Microsoft Graph (not Delegated), with admin consent:
      </Paragraph>
      <WaydGrid
        variant="simple"
        columns={permissionColumns}
        data={[...TEAMS_GRAPH_APPLICATION_PERMISSIONS]}
        getRowId={(row) => row.key}
      />
    </Card>
    <Paragraph style={{ marginTop: 16 }}>
      Full reference:{' '}
      <Link href="/docs/reference/integrations/teams">Microsoft Teams integration</Link>
    </Paragraph>
  </div>
)
