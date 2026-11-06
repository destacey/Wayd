import { Alert, Form, Input } from 'antd'
import { SecretFormItem } from './secret-form-item'
import { TeamsSetupGuide } from './teams-setup-guide'
import { ConfigSectionProps } from './azdo-configuration-section'

const { Item } = Form

export const TeamsConfigurationSection: React.FC<ConfigSectionProps> = ({
  mode,
  teamsVerifyError,
}) => {
  return (
    <>
      <TeamsSetupGuide />

      {teamsVerifyError ? (
        <Alert
          type="error"
          showIcon
          message="Connection verification failed"
          description={teamsVerifyError}
          style={{ marginBottom: 16 }}
        />
      ) : null}

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
