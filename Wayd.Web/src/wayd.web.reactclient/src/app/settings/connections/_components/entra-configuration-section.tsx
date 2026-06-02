import { EmployeeMatchProperty } from '@/src/services/wayd-api'
import { Form, FormInstance, Input, Radio, Switch } from 'antd'

const { Item } = Form

export interface ConfigSectionProps {
  form: FormInstance
  mode: 'create' | 'edit'
}

export const EntraConfigurationSection: React.FC<ConfigSectionProps> = () => {
  return (
    <>
      <Item label="Tenant ID" name="tenantId" rules={[{ required: true }]}>
        <Input maxLength={64} placeholder="00000000-0000-0000-0000-000000000000" />
      </Item>

      <Item label="Client ID" name="clientId" rules={[{ required: true }]}>
        <Input maxLength={64} placeholder="00000000-0000-0000-0000-000000000000" />
      </Item>

      <Item label="Client Secret" name="clientSecret" rules={[{ required: true }]}>
        <Input.Password maxLength={512} />
      </Item>

      <Item
        label="All Users Group Object ID"
        name="allUsersGroupObjectId"
        extra="Optional. When set, only users in this Entra group are synced. Leave blank to sync all member users in the tenant."
      >
        <Input maxLength={64} />
      </Item>

      <Item
        label="Match Employees By"
        name="matchBy"
        initialValue={EmployeeMatchProperty.Email}
        extra="Which uniquely-indexed Employee field the sync upsert matches on. Email is the cross-source-stable choice."
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
        label="Include Disabled Users"
        name="includeDisabledUsers"
        valuePropName="checked"
        initialValue={false}
      >
        <Switch />
      </Item>

      <Item
        label="Normalize Name Casing"
        name="normalizeNameCasing"
        valuePropName="checked"
        initialValue={true}
        extra="When enabled, names from Entra that come back in all-caps are title-cased before storage. Mixed-case names are preserved untouched. Handles prefixes like O', Mc, Mac, and hyphenated names correctly."
      >
        <Switch />
      </Item>
    </>
  )
}
