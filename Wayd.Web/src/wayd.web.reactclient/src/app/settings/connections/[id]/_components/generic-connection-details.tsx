'use client'

import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { Col, Descriptions, Row, Typography } from 'antd'

const { Item } = Descriptions
const { Title } = Typography

interface ConfigField {
  label: string
  value: string | number | boolean | null | undefined
  sensitive?: boolean
}

interface GenericConnectionDetailsProps {
  connection: ConnectionDetailsDto
  configFields?: ConfigField[]
}

const formatValue = (value: ConfigField['value'], sensitive: boolean) => {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (sensitive) {
    // The API masks secret fields before sending them, but mark them visually
    // here too so a future code change that forgets to mask doesn't quietly
    // leak the value into the page. Bullets are an unambiguous "this is a
    // credential" signal regardless of what the server returned.
    return '••••••••'
  }
  return String(value)
}

const GenericConnectionDetails = ({
  connection,
  configFields,
}: GenericConnectionDetailsProps) => {
  if (!connection) return null

  return (
    <>
      <Row>
        <Col xs={24} md={12}>
          <Descriptions column={1}>
            <Item label="Connector">{connection.connector?.name}</Item>
            <Item label="Category">{connection.category?.name}</Item>
            <Item label="Is Active?">{connection.isActive ? 'Yes' : 'No'}</Item>
            <Item label="Is Valid Configuration?">
              {connection.isValidConfiguration ? 'Yes' : 'No'}
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
      {configFields && configFields.length > 0 && (
        <Row>
          <Col span={24}>
            <Title level={4}>Configuration</Title>
            <Descriptions column={1}>
              {configFields.map((field) => (
                <Item key={field.label} label={field.label}>
                  {formatValue(field.value, field.sensitive ?? false)}
                </Item>
              ))}
            </Descriptions>
          </Col>
        </Row>
      )}
    </>
  )
}

export default GenericConnectionDetails
