import { Form, FormInstance, Input } from 'antd'
import { SecretFormItem } from './secret-form-item'

const { Item } = Form

export interface ConfigSectionProps {
  form: FormInstance
  mode: 'create' | 'edit'
}

export const AzureOpenAIConfigurationSection: React.FC<ConfigSectionProps> = ({
  mode,
}) => {
  return (
    <>
      <Item label="Base URL" name="baseUrl" rules={[{ required: true }]}>
        <Input
          maxLength={256}
          placeholder="https://your-resource.openai.azure.com"
        />
      </Item>

      <SecretFormItem
        label="API Key"
        name="apiKey"
        maxLength={256}
        mode={mode}
      />

      <Item
        label="Deployment Name"
        name="deploymentName"
        rules={[{ required: true }]}
      >
        <Input maxLength={128} placeholder="gpt-4" />
      </Item>
    </>
  )
}
