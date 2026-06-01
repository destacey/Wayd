import {
  EmployeeMatchProperty,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { AutoComplete, Form, Input, Radio, Switch } from 'antd'
import { ConfigSectionProps } from './azdo-configuration-section'

const { Item } = Form

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
    </>
  )
}
