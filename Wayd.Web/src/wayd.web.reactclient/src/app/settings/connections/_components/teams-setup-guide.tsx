'use client'

import { Card, Table, Typography } from 'antd'
import Link from 'next/link'
import {
  TEAMS_GRAPH_APPLICATION_PERMISSIONS,
  TEAMS_GRAPH_DELEGATED_PERMISSIONS,
} from './teams-permissions'

const { Paragraph, Title } = Typography

const permissionColumns = [
  {
    title: 'Permission',
    dataIndex: 'permission',
    key: 'permission',
    render: (value: string) => <strong>{value}</strong>,
  },
  {
    title: 'Why Wayd needs it',
    dataIndex: 'reason',
    key: 'reason',
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
        Add the <strong>Delegated</strong> permission listed below on the same app registration.
      </li>
      <li>
        Under <strong>Expose an API</strong> on the Teams app, set the Application ID URI to{' '}
        <strong>api://</strong> plus the Teams client id, add a delegated scope named{' '}
        <strong>access_as_user</strong>, and set the access token version to <strong>2</strong>.
      </li>
      <li>
        On Wayd's <strong>sign-in client</strong> (the identity provider client id, not the
        Wayd API app), add that <strong>access_as_user</strong> permission and{' '}
        <strong>Grant admin consent</strong>.
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
      <Table
        columns={permissionColumns}
        dataSource={[...TEAMS_GRAPH_APPLICATION_PERMISSIONS]}
        pagination={false}
        bordered
        size="small"
        rowKey="key"
      />
    </Card>
    <Card
      title="Delegated permission"
      size="small"
      style={{ marginTop: 12 }}
      styles={{ body: { paddingTop: 12 } }}
    >
      <Paragraph style={{ marginBottom: 12 }}>
        Delegated permission on Microsoft Graph for the signed-in Wayd user. Not an application
        permission. Verify and Test connection use it to post the hello-world chat message:
      </Paragraph>
      <Table
        columns={permissionColumns}
        dataSource={[...TEAMS_GRAPH_DELEGATED_PERMISSIONS]}
        pagination={false}
        bordered
        size="small"
        rowKey="key"
      />
    </Card>
    <Paragraph style={{ marginTop: 16 }}>
      Full reference:{' '}
      <Link href="/docs/reference/integrations/teams">Microsoft Teams integration</Link>
    </Paragraph>
  </div>
)
