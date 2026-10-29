'use client'

import { Typography } from 'antd'
import Link from 'next/link'

const { Paragraph, Title } = Typography

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
        In Wayd, open <strong>Settings → System → Connections</strong> and create or edit a{' '}
        <strong>Microsoft Teams</strong> connection with the tenant ID, client ID, and secret.
      </li>
      <li>
        On the connection detail page, choose <strong>Test connection</strong> in the Actions menu.
        Wayd sends you a Teams chat message and creates a hello-world all-day calendar event using
        the email on your Wayd account. If Graph cannot find that user in the tenant, the test
        fails and creates nothing else.
      </li>
    </ol>
    <Title level={5}>Required permissions</Title>
    <Paragraph>
      Application permissions on Microsoft Graph (not Delegated), with admin consent:
    </Paragraph>
    <table>
      <thead>
        <tr>
          <th>Permission</th>
          <th>Why Wayd needs it</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>
            <strong>User.Read.All</strong>
          </td>
          <td>Find your Wayd account email in the tenant before sending the test message and event.</td>
        </tr>
        <tr>
          <td>
            <strong>Chat.Create</strong>
          </td>
          <td>Create the short-lived Teams chat used for the hello-world message.</td>
        </tr>
        <tr>
          <td>
            <strong>ChatMessage.Send</strong>
          </td>
          <td>Post the hello-world Teams message in that chat.</td>
        </tr>
        <tr>
          <td>
            <strong>Calendars.ReadWrite</strong>
          </td>
          <td>Create the hello-world all-day calendar event on your calendar.</td>
        </tr>
      </tbody>
    </table>
    <Paragraph style={{ marginTop: 16 }}>
      Full reference:{' '}
      <Link href="/docs/reference/integrations/teams">Microsoft Teams integration</Link>
    </Paragraph>
  </div>
)
