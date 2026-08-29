import { AzureDevOpsConnectionConfigurationDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'

const { Item } = Descriptions

interface AzdoConnectionConfigurationProps {
  configuration: AzureDevOpsConnectionConfigurationDto
}

const AzdoConnectionConfiguration = ({
  configuration,
}: AzdoConnectionConfigurationProps) => {
  if (!configuration) return null

  return (
    <>
      <Descriptions column={1}>
        <Item label="Organization">{configuration.organization}</Item>
        <Item label="PAT">{configuration.personalAccessToken}</Item>
      </Descriptions>
    </>
  )
}

export default AzdoConnectionConfiguration
