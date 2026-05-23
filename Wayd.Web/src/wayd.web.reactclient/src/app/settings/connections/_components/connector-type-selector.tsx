import {
  ConnectorType,
  CONNECTOR_DESCRIPTIONS,
  CONNECTOR_NAMES,
} from '@/src/types/connectors'
import { Card, Col, Row, Typography } from 'antd'

const { Title, Text } = Typography

interface ConnectorTypeSelectorProps {
  onSelect: (type: ConnectorType) => void
}

export const ConnectorTypeSelector: React.FC<ConnectorTypeSelectorProps> = ({
  onSelect,
}) => {
  return (
    <>
      <Title level={4} style={{ marginBottom: 16 }}>
        Select Connector Type
      </Title>

      <Row gutter={[16, 16]}>
        {Object.values(ConnectorType)
          .filter(
            (v) =>
              typeof v === 'number' && v !== ConnectorType.OpenAI, // Exclude OpenAI for now
          )
          .map((type) => (
            <Col key={type} span={12}>
              <Card
                hoverable
                onClick={() => onSelect(type as ConnectorType)}
                style={{ cursor: 'pointer', height: '100%' }}
                styles={{ body: { padding: 16 } }}
              >
                <Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
                  {CONNECTOR_NAMES[type as ConnectorType]}
                </Title>
                <Text type="secondary" style={{ fontSize: 13 }}>
                  {CONNECTOR_DESCRIPTIONS[type as ConnectorType]}
                </Text>
              </Card>
            </Col>
          ))}
      </Row>
    </>
  )
}
