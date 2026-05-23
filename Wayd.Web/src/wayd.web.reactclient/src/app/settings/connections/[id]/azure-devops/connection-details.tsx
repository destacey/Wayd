import { AzureDevOpsConnectionDetailsDto } from '@/src/services/wayd-api'
import { Col, Descriptions, Row, Typography } from 'antd'
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
    <>
      <Row>
        <Col xs={24} md={12}>
          <Descriptions column={1}>
            <Item label="System Id">{connection.systemId}</Item>
            <Item label="Connector">{connection.connector.name}</Item>
            <Item label="Is Active?">{connection.isActive ? 'Yes' : 'No'}</Item>
            <Item label="Is Valid Configuration?">
              {connection.isValidConfiguration ? 'Yes' : 'No'}
            </Item>
            <Item label="Is Sync Enabled?">
              {connection.isSyncEnabled ? 'Yes' : 'No'}
            </Item>
          </Descriptions>
        </Col>
        <Col xs={24} md={12}>
          <Descriptions layout="vertical">
            <Item label="Description">
              <MarkdownRenderer markdown={connection.description} />
            </Item>
          </Descriptions>
        </Col>
      </Row>
      <Row>
        <Title level={4}>Configuration</Title>
        <AzdoConnectionConfiguration configuration={connection.configuration} />
      </Row>
    </>
  )
}

export default AzdoConnectionDetails
