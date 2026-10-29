import { Form, FormInstance, Input } from 'antd'
import { SecretFormItem } from './secret-form-item'
import { TeamsSetupGuide } from './teams-setup-guide'

const { Item } = Form

export interface ConfigSectionProps {
  form: FormInstance
  mode: 'create' | 'edit'
}

export const TeamsConfigurationSection: React.FC<ConfigSectionProps> = ({ mode }) => {
  return (
    <>
      <TeamsSetupGuide />

      <Item label="Tenant ID" name="tenantId" rules={[{ required: true }]}>
        <Input maxLength={64} placeholder="00000000-0000-0000-0000-000000000000" />
      </Item>

      <Item label="Client ID" name="clientId" rules={[{ required: true }]}>
        <Input maxLength={64} placeholder="00000000-0000-0000-0000-000000000000" />
      </Item>

      <SecretFormItem
        label="Client Secret"
        name="clientSecret"
        maxLength={512}
        mode={mode}
      />
    </>
  )
}
