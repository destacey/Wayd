import { getConnectionsClient } from '@/src/services/clients'
import {
  DiscoveredOrg,
  EmployeeMatchProperty,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { CloseOutlined, PlusOutlined } from '@ant-design/icons'
import {
  AutoComplete,
  Button,
  Form,
  Input,
  Radio,
  Select,
  Spin,
  Switch,
  Typography,
} from 'antd'
import { useCallback, useState } from 'react'
import { ConfigSectionProps } from './azdo-configuration-section'

const { Item } = Form
const { Text } = Typography

/**
 * Builds the primary (headline) + secondary (muted) text for an org row in the exclusion picker.
 * Prefers Name → Reference_ID → WID for the headline; the secondary line lists whichever of
 * (Reference_ID, WID) are present AND distinct from the headline. Some Workday tenants populate
 * Reference_ID with the same string as Name — in that case we drop it to avoid "All Companies / All Companies • 71aa…".
 */
const buildOrgLabelParts = (o: {
  reference: string
  displayName?: string | null
  referenceId?: string | null
}): { primary: string; secondary: string } => {
  const primary = o.displayName ?? o.referenceId ?? o.reference
  const candidates = [o.displayName, o.referenceId, o.reference]
  const seen = new Set<string>([primary])
  const secondaryParts: string[] = []
  for (const c of candidates) {
    if (!c || seen.has(c)) continue
    seen.add(c)
    secondaryParts.push(c)
  }
  return { primary, secondary: secondaryParts.join(' • ') }
}

export const WorkdayConfigurationSection: React.FC<ConfigSectionProps> = ({
  connection,
}) => {
  // Pull the discovered catalog from the connection (only present on edit, only populated by a
  // successful init probe). When empty, the AutoComplete renders with no suggestions and the
  // admin can still type any value they know about — e.g. on Create before any probe has run.
  const workday = connection as WorkdayConnectionDetailsDto | undefined
  const discoveredOrgTypes = workday?.configuration?.discoveredOrgTypes ?? []

  // Each option's value is the stable Workday Organization_Type_ID (what the API expects). The
  // label shows the friendly Descriptor and the worker-count so admins can pick a type that
  // actually has data in their tenant.
  const orgTypeOptions = discoveredOrgTypes.map((t) => ({
    value: t.typeId,
    label: `${t.typeId}${t.displayName ? ` — ${t.displayName}` : ''} (${t.count} org${t.count === 1 ? '' : 's'})`,
  }))

  return (
    <>
      <Item
        label="WSDL URL"
        name="wsdlUrl"
        rules={[{ required: true }]}
        extra="Paste the WSDL URL from Workday's 'View API Clients' screen — e.g. https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1?wsdl"
      >
        <Input
          maxLength={1024}
          placeholder="https://{host}/ccx/service/{tenant}/Staffing/v46.1"
        />
      </Item>

      <Item
        label="ISU Username"
        name="isuUsername"
        rules={[{ required: true }]}
        extra="Conventionally formatted as `{user}@{tenant}`."
      >
        <Input maxLength={256} placeholder="wayd_isu@acme_corp1" />
      </Item>

      <Item
        label="ISU Password"
        name="isuPassword"
        rules={[{ required: true }]}
      >
        <Input.Password maxLength={512} />
      </Item>

      <Item
        label="Worker Key"
        name="workerKey"
        initialValue={WorkdayWorkerKey.EmployeeId}
        extra="Which Workday identifier maps onto Employee.EmployeeNumber. The Workday Worker ID (WID) is immutable but opaque; the Employee ID is human-readable."
      >
        <Radio.Group>
          <Radio.Button value={WorkdayWorkerKey.EmployeeId}>
            Employee ID
          </Radio.Button>
          <Radio.Button value={WorkdayWorkerKey.Wid}>
            Workday Worker ID
          </Radio.Button>
        </Radio.Group>
      </Item>

      <Item
        label="Match Employees By"
        name="matchBy"
        initialValue={EmployeeMatchProperty.Email}
        extra="Which uniquely-indexed Employee field the sync upsert matches on. Email is the cross-source-stable choice — keep it unless both connectors emit the same stable employee number."
      >
        <Radio.Group>
          <Radio.Button value={EmployeeMatchProperty.Email}>
            Email
          </Radio.Button>
          <Radio.Button value={EmployeeMatchProperty.EmployeeNumber}>
            Employee Number
          </Radio.Button>
        </Radio.Group>
      </Item>

      <Item
        label="Include Inactive Workers"
        name="includeInactive"
        valuePropName="checked"
        initialValue={false}
        extra="When enabled, terminated/inactive workers are also returned by the sync."
      >
        <Switch />
      </Item>

      <Item
        label="Use User_ID as Email Fallback"
        name="useUserIdAsEmailFallback"
        valuePropName="checked"
        initialValue={false}
        extra="When enabled, sync uses Workday's User_ID (the account login) as the work email if Contact_Data is missing — but only when it parses as a valid email. Use for tenants whose ISU is not granted 'Worker Data: Personal Contact Information'."
      >
        <Switch />
      </Item>

      <Item
        label="Use Preferred Name"
        name="usePreferredName"
        valuePropName="checked"
        initialValue={false}
        extra="When enabled, sync reads each worker's Preferred Name (first/middle/last) in preference to Legal Name, falling back to legal per-component when a preferred component is missing."
      >
        <Switch />
      </Item>

      <Item
        label="Normalize Name Casing"
        name="normalizeNameCasing"
        valuePropName="checked"
        initialValue={true}
        extra="When enabled, names from Workday that come back in all-caps (a common HRIS convention) are title-cased before storage. Mixed-case names are preserved untouched. Handles prefixes like O', Mc, Mac, and hyphenated names correctly."
      >
        <Switch />
      </Item>

      <Item
        label="Department Source"
        name="departmentOrganizationTypeId"
        initialValue="SUPERVISORY"
        extra={
          orgTypeOptions.length > 0
            ? "Workday Organization_Type_ID that drives Employee.Department. Pick from the discovered catalog or type a custom type ID. Leave blank to skip Department sync."
            : "Workday Organization_Type_ID that drives Employee.Department. Default 'SUPERVISORY' (Workday's reporting hierarchy — present in every tenant). After the first init probe runs, this field will suggest the types discovered in your tenant. Leave blank to skip Department sync."
        }
      >
        <AutoComplete
          options={orgTypeOptions}
          placeholder="SUPERVISORY"
          allowClear
          // Filter the dropdown as the admin types — match on the type ID (the value).
          // Display label includes the count for context, but search by ID keeps it predictable.
          filterOption={(input, option) =>
            (option?.value as string)
              ?.toLowerCase()
              .includes(input.toLowerCase()) ?? false
          }
          maxLength={128}
        />
      </Item>

      <ExclusionsSection
        connectionId={workday?.id}
        orgTypeOptions={orgTypeOptions}
      />
    </>
  )
}

/**
 * Repeatable list of "exclude workers in org X" rules. Each row is a (type, org) pair:
 *   - Type picker reads from the same discovered-catalog the Department Source uses.
 *   - Org picker lazy-loads from /workday/orgs?typeId=… once the admin picks a type; that
 *     keeps the create form light when the tenant has thousands of supervisory orgs.
 *
 * Create mode has no connectionId, so the org picker stays disabled until the admin saves and
 * comes back to edit. We surface that explicitly so it's not just a silently-empty dropdown.
 */
const ExclusionsSection = ({
  connectionId,
  orgTypeOptions,
}: {
  connectionId: string | undefined
  orgTypeOptions: { value: string; label: string }[]
}) => {
  return (
    <Form.List name="orgExclusions">
      {(fields, { add, remove }) => (
        <div style={{ marginTop: 24 }}>
          <Text strong>Exclude Workers In</Text>
          <div
            style={{
              fontSize: 13,
              color: 'var(--ant-color-text-secondary)',
              marginBottom: 8,
            }}
          >
            Workers belonging to any org listed here are dropped from the sync.
            Useful for excluding contractors, holding companies, or
            sandbox/training orgs. Each rule names an Organization Type and a
            specific org of that type.
            {!connectionId && (
              <>
                {' '}
                <strong>
                  Save the connection first to enable the org picker.
                </strong>
              </>
            )}
          </div>

          {fields.map((field) => (
            <ExclusionRow
              key={field.key}
              fieldName={field.name}
              connectionId={connectionId}
              orgTypeOptions={orgTypeOptions}
              onRemove={() => remove(field.name)}
            />
          ))}

          <Button
            type="dashed"
            onClick={() =>
              add({
                organizationTypeId: undefined,
                organizationReference: undefined,
                displayName: undefined,
              })
            }
            block
            icon={<PlusOutlined />}
            style={{ marginTop: 8 }}
          >
            Add Exclusion
          </Button>
        </div>
      )}
    </Form.List>
  )
}

const ExclusionRow = ({
  fieldName,
  connectionId,
  orgTypeOptions,
  onRemove,
}: {
  fieldName: number
  connectionId: string | undefined
  orgTypeOptions: { value: string; label: string }[]
  onRemove: () => void
}) => {
  const form = Form.useFormInstance()
  // Watch the type field via dependency lookup so the org picker re-fetches when the admin
  // switches types in this specific row. useWatch on a deeply-nested Form.List path uses an
  // array tuple for the path.
  const selectedType = Form.useWatch(
    ['orgExclusions', fieldName, 'organizationTypeId'],
    form,
  )

  const [orgs, setOrgs] = useState<DiscoveredOrg[]>([])
  const [orgsLoading, setOrgsLoading] = useState(false)
  const [orgsError, setOrgsError] = useState<string | null>(null)
  const [loadedForType, setLoadedForType] = useState<string | null>(null)

  // Lazy-fetch the orgs for the picker the first time the dropdown opens (or when the type
  // changes). We don't kick this off on render so a form with several empty rows doesn't fire
  // N parallel SOAP-backed requests on mount.
  const ensureOrgsLoaded = useCallback(async () => {
    if (!connectionId || !selectedType) return
    if (loadedForType === selectedType) return
    setOrgsLoading(true)
    setOrgsError(null)
    try {
      const result = await getConnectionsClient().getWorkdayOrgsByType(
        connectionId,
        selectedType,
      )
      setOrgs(result ?? [])
      setLoadedForType(selectedType)
    } catch (e) {
      console.error(e)
      setOrgsError(
        'Could not load orgs for this type. Try Test Connection on the detail page.',
      )
    } finally {
      setOrgsLoading(false)
    }
  }, [connectionId, selectedType, loadedForType])

  // Each inner field has a sensible minimum width — when the row can't fit both side-by-side
  // (mobile, narrow viewports) flex-wrap drops the org picker onto its own line. The remove
  // button sticks to whichever line the org picker ends up on.
  return (
    <div
      style={{
        marginBottom: 8,
        display: 'flex',
        flexWrap: 'wrap',
        gap: 8,
        alignItems: 'flex-start',
      }}
    >
      <Item
        name={[fieldName, 'organizationTypeId']}
        rules={[{ required: true, message: 'Type required' }]}
        style={{ flex: '1 1 140px', marginBottom: 0, minWidth: 0 }}
      >
        <Select
          placeholder="Type"
          options={orgTypeOptions}
          allowClear
          // AntD 6 moved filterOption inside the showSearch object; the standalone prop is
          // deprecated. Same matcher as before — match user input against the type ID (value).
          showSearch={{
            filterOption: (input, option) =>
              (option?.value as string)
                ?.toLowerCase()
                .includes(input.toLowerCase()) ?? false,
          }}
          // Let the dropdown grow past the trigger width so long type IDs (with descriptor +
          // worker count appended) don't get truncated at the trigger's narrow column width.
          // The popup is capped at 90vw so it never overflows the viewport on mobile.
          popupMatchSelectWidth={false}
          popupRender={(menu) => (
            <div style={{ minWidth: 'min(320px, 90vw)', maxWidth: '90vw' }}>
              {menu}
            </div>
          )}
          // When the admin changes the type, clear any previously-selected org for this row
          // so we don't keep a stale (typeA, orgB) pairing.
          onChange={() => {
            form.setFields([
              {
                name: ['orgExclusions', fieldName, 'organizationReference'],
                value: undefined,
              },
              {
                name: ['orgExclusions', fieldName, 'displayName'],
                value: undefined,
              },
            ])
            setOrgs([])
            setLoadedForType(null)
          }}
        />
      </Item>
      <Item
        name={[fieldName, 'organizationReference']}
        rules={[{ required: true, message: 'Org required' }]}
        style={{ flex: '2 1 200px', marginBottom: 0, minWidth: 0 }}
      >
        <Select
          placeholder={
            !connectionId
              ? 'Save the connection first'
              : !selectedType
                ? 'Pick a type first'
                : orgsLoading
                  ? 'Loading…'
                  : orgsError
                    ? orgsError
                    : 'Pick an org'
          }
          disabled={!connectionId || !selectedType}
          // Long org names (e.g. "Cost Center: Engineering R&D — Platform Services") shouldn't
          // truncate at the trigger width either. Same 90vw cap as the type picker.
          popupMatchSelectWidth={false}
          popupRender={(menu) => (
            <div style={{ minWidth: 'min(360px, 90vw)', maxWidth: '90vw' }}>
              {menu}
            </div>
          )}
          onOpenChange={(open) => {
            if (open) void ensureOrgsLoaded()
          }}
          onChange={(value, option) => {
            // Cache the display name alongside the reference so the sync log + detail page can
            // read it without a round-trip to Workday. We use the plain Name string here (not the
            // rich JSX label rendered below) so the cache is a clean string round-trip.
            const picked = Array.isArray(option) ? undefined : option
            const cached =
              (picked?.['data-name'] as string | undefined) ??
              (picked?.['data-reference-id'] as string | undefined) ??
              (value as string)
            form.setFieldValue(
              ['orgExclusions', fieldName, 'displayName'],
              cached,
            )
          }}
          options={orgs.map((o) => {
            const { primary, secondary } = buildOrgLabelParts(o)
            return {
              value: o.reference,
              // Searchable string — used by showSearch.filterOption and by AntD when rendering
              // the selected value in the trigger (rich JSX won't render once selected on a
              // non-mode select).
              label: secondary ? `${primary} ${secondary}` : primary,
              // Custom data props the onChange handler reads to cache a clean name.
              'data-name': o.displayName ?? undefined,
              'data-reference-id': o.referenceId ?? undefined,
            }
          })}
          optionRender={(option) => {
            const o = orgs.find((x) => x.reference === option.value)
            if (!o) return option.label
            const { primary, secondary } = buildOrgLabelParts(o)
            return (
              <div style={{ lineHeight: 1.3, padding: '2px 0' }}>
                <div>{primary}</div>
                {secondary && (
                  <div
                    style={{
                      fontSize: 12,
                      color: 'var(--ant-color-text-tertiary)',
                    }}
                  >
                    {secondary}
                  </div>
                )}
              </div>
            )
          }}
          notFoundContent={orgsLoading ? <Spin size="small" /> : undefined}
          // AntD 6 moved filterOption inside the showSearch object; the standalone prop is
          // deprecated. Match the searchable label string (Name + ReferenceId + WID) so admins
          // can find an org by any of those identifiers.
          showSearch={{
            filterOption: (input, option) =>
              String(option?.label ?? option?.value)
                .toLowerCase()
                .includes(input.toLowerCase()),
          }}
        />
      </Item>
      {/* Hidden carrier for the cached display name. Form.List submits this row as a single object,
          and the org-picker onChange above writes the label here so it round-trips on save. */}
      <Item name={[fieldName, 'displayName']} hidden noStyle>
        <Input type="hidden" />
      </Item>
      <Button
        type="text"
        danger
        icon={<CloseOutlined />}
        onClick={onRemove}
        aria-label="Remove exclusion"
      />
    </div>
  )
}
