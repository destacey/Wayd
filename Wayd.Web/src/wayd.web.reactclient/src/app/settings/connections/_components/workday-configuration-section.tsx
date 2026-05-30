import {
  EmployeeMatchProperty,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { Form, Input, Radio, Switch } from 'antd'
import { ConfigSectionProps } from './azdo-configuration-section'

const { Item } = Form

export const WorkdayConfigurationSection: React.FC<ConfigSectionProps> = () => {
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
        label="Incremental Sync"
        name="incrementalSyncEnabled"
        valuePropName="checked"
        initialValue={true}
        extra="After the first full sync, subsequent runs fetch only workers changed since the last successful run."
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
    </>
  )
}
