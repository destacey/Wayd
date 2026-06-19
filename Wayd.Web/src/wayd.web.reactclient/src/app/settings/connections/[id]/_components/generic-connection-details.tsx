'use client'

import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { getCapabilityNames } from '@/src/types/connectors'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { ResponsiveFlex } from '@/src/components/common'
import { Descriptions, Flex, Typography } from 'antd'

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
    <Flex vertical gap="middle">
      <ResponsiveFlex>
        <Descriptions column={1}>
          <Item label="Connector">{connection.connector?.name}</Item>
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
      {configFields && configFields.length > 0 && (
        <div>
          <Title level={4}>Configuration</Title>
          <Descriptions column={1}>
            {configFields.map((field) => (
              <Item key={field.label} label={field.label}>
                {formatValue(field.value, field.sensitive ?? false)}
              </Item>
            ))}
          </Descriptions>
        </div>
      )}
    </Flex>
  )
}

export default GenericConnectionDetails
