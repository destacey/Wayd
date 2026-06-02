import {
  ConnectionDetailsDto,
  TestAzureDevOpsConnectionRequest,
} from '@/src/services/wayd-api'
import { useTestAzdoConfigurationMutation } from '@/src/store/features/app-integration/azdo-integration-api'
import { Button, Form, FormInstance, Input, Typography } from 'antd'
import { useState } from 'react'

const { Item } = Form
const { Text } = Typography

export interface ConfigSectionProps {
  form: FormInstance
  mode: 'create' | 'edit'
  /**
   * The existing connection being edited. Undefined on the create form. Sections that need to
   * read connection-derived state (e.g. Workday's discovered org-types catalog populated by the
   * init probe) consume this; sections that don't can ignore it.
   */
  connection?: ConnectionDetailsDto
}

export const AzureDevOpsConfigurationSection: React.FC<ConfigSectionProps> = ({
  form,
}) => {
  const [testConfigurationResult, setTestConfigurationResult] =
    useState<string>()
  const [isTestingConfiguration, setTestingConfiguration] = useState(false)

  const [testConfig] = useTestAzdoConfigurationMutation()

  const testConnectionConfiguration = async (configuration: TestAzureDevOpsConnectionRequest) => {
    const response = await testConfig(configuration)
    if (response.error) {
      setTestConfigurationResult('Failed to test configuration.')
    } else {
      setTestConfigurationResult('Successfully tested configuration.')
    }
    setTestingConfiguration(false)
  }

  return (
    <>
      <Item label="Organization" name="organization" rules={[{ required: true }]}>
        <Input showCount maxLength={128} />
      </Item>
      <Item
        label="Personal Access Token"
        name="personalAccessToken"
        rules={[{ required: true }]}
      >
        <Input showCount maxLength={128} />
      </Item>

      <Item>
        <Button
          type="primary"
          disabled={
            !form.getFieldValue('organization') ||
            !form.getFieldValue('personalAccessToken')
          }
          loading={isTestingConfiguration}
          onClick={() => {
            setTestingConfiguration(true)
            testConnectionConfiguration({
              organization: form.getFieldValue('organization'),
              personalAccessToken: form.getFieldValue('personalAccessToken'),
            })
          }}
        >
          Test Configuration
        </Button>
        <Text type="secondary" style={{ marginLeft: '10px' }}>
          {testConfigurationResult}
        </Text>
      </Item>
    </>
  )
}
