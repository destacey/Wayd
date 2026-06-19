import { AzureDevOpsConnectionDetailsDto } from '@/src/services/wayd-api'
import { getCapabilityNames } from '@/src/types/connectors'
import { ResponsiveFlex } from '@/src/components/common'
import { Descriptions, Flex, Typography } from 'antd'
import AzdoConnectionConfiguration from './connection-configuration'
import { MarkdownRenderer } from '@/src/components/common/markdown'

const { Item } = Descriptions
const { Title } = Typography

interface AzdoConnectionDetailsProps {
  connection: AzureDevOpsConnectionDetailsDto
}

const AzdoConnectionDetails = ({ connection }: AzdoConnectionDetailsProps) => {
  if (!connection) return null
  return (
    <Flex vertical gap="middle">
      <ResponsiveFlex>
        <Descriptions column={1}>
          <Item label="System Id">{connection.systemId}</Item>
          <Item label="Connector">{connection.connector.name}</Item>
          <Item label="Capabilities">{getCapabilityNames(connection)}</Item>
          <Item label="Is Active?">{connection.isActive ? 'Yes' : 'No'}</Item>
          <Item label="Is Valid Configuration?">
            {connection.isValidConfiguration ? 'Yes' : 'No'}
          </Item>
        </Descriptions>
        <Descriptions layout="vertical">
          <Item label="Description">
            <MarkdownRenderer markdown={connection.description} />
          </Item>
        </Descriptions>
      </ResponsiveFlex>
      <div>
        <Title level={4}>Configuration</Title>
        <AzdoConnectionConfiguration configuration={connection.configuration} />
      </div>
    </Flex>
  )
}

export default AzdoConnectionDetails
