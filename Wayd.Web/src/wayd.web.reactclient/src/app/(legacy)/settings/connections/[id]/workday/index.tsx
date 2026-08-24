'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionDetailsDto,
  EmployeeMatchProperty,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { useInitConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { Alert, Col, Descriptions, Row, Typography } from 'antd'
import dayjs from 'dayjs'
import { ItemType } from 'antd/es/menu/interface'
import { useEffect, useMemo } from 'react'
import {
  ConnectionActionContext,
  DetailEntry,
} from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'
import SyncHistoryTab from '../_components/sync-history-tab'

const { Item } = Descriptions
const { Title } = Typography

const isWorkday = (c: ConnectionDetailsDto): c is WorkdayConnectionDetailsDto =>
  c?.connector?.name === 'Workday'

const workerKeyLabel = (key: WorkdayWorkerKey | undefined): string => {
  switch (key) {
    case WorkdayWorkerKey.Wid:
      return 'Workday Worker ID'
    case WorkdayWorkerKey.EmployeeId:
      return 'Employee ID'
    default:
      return '—'
  }
}

const InitResultCallout = ({
  connection,
}: {
  connection: WorkdayConnectionDetailsDto
}) => {
  const config = connection.configuration
  if (!config?.lastInitAt) return null

  if (config.lastInitSucceeded) {
    if ((config.lastInitWarnings?.length ?? 0) === 0) return null
    return (
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        title="Connection is valid, but the probe noted some observations:"
        description={
          <ul style={{ marginBottom: 0 }}>
            {config.lastInitWarnings?.map((w, i) => <li key={i}>{w}</li>)}
          </ul>
        }
      />
    )
  }

  return (
    <Alert
      type="error"
      showIcon
      style={{ marginBottom: 16 }}
      title="This connection's configuration is invalid. Sync is disabled until resolved."
      description={
        <>
          {config.lastInitAuthError && <p>{config.lastInitAuthError}</p>}
          {(config.lastInitMissingFields?.length ?? 0) > 0 && (
            <>
              <p>
                The Integration System User could not read these required
                fields — likely an Integration System Security Group (ISSG) gap
                in Workday:
              </p>
              <ul style={{ marginBottom: 0 }}>
                {config.lastInitMissingFields?.map((f, i) => <li key={i}>{f}</li>)}
              </ul>
            </>
          )}
          {(config.lastInitWarnings?.length ?? 0) > 0 && (
            <>
              <p>Additional warnings:</p>
              <ul style={{ marginBottom: 0 }}>
                {config.lastInitWarnings?.map((w, i) => <li key={i}>{w}</li>)}
              </ul>
            </>
          )}
        </>
      }
    />
  )
}

/**
 * Renders the admin-configured exclusion rules. Each rule shows the org type, the cached display
 * name (or the WID if no label is on file), and a hint about what it filters out at sync time.
 * Null when no rules are configured — keeps the detail page quiet for the default case.
 */
const ExclusionsPanel = ({
  connection,
}: {
  connection: WorkdayConnectionDetailsDto
}) => {
  const exclusions = connection.configuration?.orgExclusions ?? []
  if (exclusions.length === 0) return null
  return (
    <div style={{ marginTop: 16, marginBottom: 16 }}>
      <strong>Worker Exclusions</strong>
      <div
        style={{
          fontSize: 13,
          color: 'var(--ant-color-text-secondary)',
          marginBottom: 8,
        }}
      >
        Workers in any of these orgs are dropped from the sync. See the Sync
        History tab for per-run exclusion counts.
      </div>
      <ul style={{ marginBottom: 0, paddingInlineStart: 20 }}>
        {exclusions.map((e) => (
          <li
            key={`${e.organizationTypeId}:${e.organizationReference}`}
          >
            <code>{e.organizationTypeId}</code>
            {e.displayName ? `: ${e.displayName}` : ''}
            <span
              style={{
                marginLeft: 8,
                fontSize: 12,
                color: 'var(--ant-color-text-tertiary)',
              }}
            >
              ({e.organizationReference})
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

/**
 * Renders the catalog of Organization_Type_IDs discovered by the most recent init probe. Helps
 * admins pick a non-default value for "Department Source" — they can see which types actually
 * have data (the Count) and what each is labeled in their tenant (DisplayName).
 */
const DiscoveredOrgTypesPanel = ({
  connection,
}: {
  connection: WorkdayConnectionDetailsDto
}) => {
  const types = connection.configuration?.discoveredOrgTypes
  if (!types || types.length === 0) return null

  const selected = connection.configuration?.departmentOrganizationTypeId
  return (
    <div style={{ marginTop: 16, marginBottom: 16 }}>
      <strong>Discovered Organization Types</strong>
      <div style={{ fontSize: 13, color: 'var(--ant-color-text-secondary)', marginBottom: 8 }}>
        Available values for the Department Source. Edit the connection to switch.
      </div>
      <ul style={{ marginBottom: 0, paddingInlineStart: 20 }}>
        {types.map((t) => (
          <li key={t.typeId}>
            <code>{t.typeId}</code>
            {t.displayName ? ` — ${t.displayName}` : ''}
            {` (${t.count} org${t.count === 1 ? '' : 's'})`}
            {selected === t.typeId && (
              <strong style={{ marginLeft: 8 }}>← in use</strong>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isWorkday(connection)) return null
  const config = connection.configuration

  return (
    <>
      {/* The init callout stays at the top: a failed connection's error message must be the
          first thing the admin sees. */}
      <InitResultCallout connection={connection} />

      {/* Top metadata (Connector / Category / IsActive / IsValid / Description). We omit
          configFields here so GenericConnectionDetails doesn't emit its own Configuration
          section — we own that block below so the exclusion + discovered-types panels can sit
          inside the same heading as the field list. */}
      <GenericConnectionDetails connection={connection} />

      <Row>
        <Col span={24}>
          <Title level={4}>Configuration</Title>
          <Descriptions column={1}>
            <Item label="WSDL URL">{config?.wsdlUrl ?? '—'}</Item>
            <Item label="Service Host">{config?.serviceHost ?? '—'}</Item>
            <Item label="Tenant Alias">{config?.tenantAlias ?? '—'}</Item>
            <Item label="WSDL Version">{config?.wsdlVersion ?? '—'}</Item>
            <Item label="ISU Username">{config?.isuUsername ?? '—'}</Item>
            <Item label="ISU Password">••••••••</Item>
            <Item label="Worker Key">{workerKeyLabel(config?.workerKey)}</Item>
            <Item label="Match Employees By">
              {config?.matchBy === EmployeeMatchProperty.EmployeeNumber
                ? 'Employee Number'
                : 'Email'}
            </Item>
            <Item label="Include Inactive Workers">
              {config?.includeInactive ? 'Yes' : 'No'}
            </Item>
            <Item label="Use User_ID as Email Fallback">
              {config?.useUserIdAsEmailFallback ? 'Yes' : 'No'}
            </Item>
            <Item label="Use Preferred Name">
              {config?.usePreferredName ? 'Yes' : 'No'}
            </Item>
            <Item label="Normalize Name Casing">
              {config?.normalizeNameCasing ? 'Yes' : 'No'}
            </Item>
            <Item label="Department Source (Organization_Type_ID)">
              {config?.departmentOrganizationTypeId ?? '—'}
            </Item>
            <Item label="Last Validated">
              {config?.lastInitAt
                ? dayjs(config.lastInitAt).format('M/D/YYYY h:mm A')
                : '—'}
            </Item>
          </Descriptions>

          {/* Configuration-section panels: same visual block as the field list above, separated
              from each other by their own headings. Exclusions first because they're the
              actionable filter the admin set up; discovered types is a reference list. */}
          <ExclusionsPanel connection={connection} />
          <DiscoveredOrgTypesPanel connection={connection} />
        </Col>
      </Row>
    </>
  )
}

// Adds a "Test Connection" entry to the page shell's Actions dropdown. We model it as a plain
// menu item (text label + onClick) rather than embedding a <Button> in the label — the shell
// renders the menu with antd's Menu, which would otherwise show a button-inside-a-menu-row.
const ExtraActions = ({
  ctx,
  setItems,
}: {
  ctx: ConnectionActionContext
  setItems: (items: ItemType[]) => void
}) => {
  const messageApi = useMessage()
  const [initConnection] = useInitConnectionMutation()

  const items = useMemo<ItemType[]>(() => {
    const testConnection = async () => {
      try {
        const response = await initConnection(ctx.connectionId)
        if ('error' in response) throw response.error
        const result = response.data
        if (result.isValid) {
          messageApi.success(
            `Connection validated — probed ${result.workersProbed} worker(s).`,
          )
        } else if (result.authError) {
          messageApi.error(`Authentication failed: ${result.authError}`)
        } else {
          messageApi.error(
            `Configuration is invalid (${result.missingRequiredFields.length} missing field(s)). See the connection details for specifics.`,
          )
        }
        ctx.reload()
      } catch (e) {
        console.error(e)
        messageApi.error('Failed to run the Workday connection probe.')
      }
    }

    return [
      {
        key: 'test-connection',
        label: 'Test Connection',
        disabled: !ctx.canUpdate,
        onClick: testConnection,
      },
    ]
  }, [ctx, initConnection, messageApi])

  // setItems updates state on the parent page; React forbids calling it during render of a
  // child, so we defer to an effect.
  useEffect(() => {
    setItems(items)
  }, [items, setItems])

  return null
}

export const workdayDetailEntry: DetailEntry = {
  Details,
  ExtraActions,
  extraTabs: [
    {
      key: 'sync-history',
      label: 'Sync History',
      render: (connection) => (
        <SyncHistoryTab
          connectionId={connection.id}
          category="people"
          isActive={connection.isActive}
        />
      ),
    },
  ],
}
