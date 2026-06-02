import { MarkdownEditor } from '@/src/components/common/markdown'
import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import {
  ConnectorType,
  CONNECTOR_NAMES,
} from '@/src/types/connectors'
import { Divider, Form, FormInstance, Input } from 'antd'
import { CONNECTOR_FORM_REGISTRY } from './connector-registry'

const { Item } = Form
const { TextArea } = Input

export interface ConnectionFormBaseProps {
  connector: ConnectorType
  mode: 'create' | 'edit'
  form: FormInstance
  /** The existing connection being edited; undefined on create. Forwarded to ConfigurationSection. */
  connection?: ConnectionDetailsDto
}

export const ConnectionFormBase: React.FC<ConnectionFormBaseProps> = ({
  connector,
  mode,
  form,
  connection,
}) => {
  const ConfigurationSection = CONNECTOR_FORM_REGISTRY[connector]

  return (
    <>
      {/* Shared fields for all connectors */}
      <Item label="Name" name="name" rules={[{ required: true }]}>
        <TextArea
          autoSize={{ minRows: 1, maxRows: 2 }}
          showCount
          maxLength={128}
        />
      </Item>

      <Item
        name="description"
        label="Description"
        initialValue=""
        rules={[{ max: 1024 }]}
      >
        <MarkdownEditor
          value={form.getFieldValue('description')}
          onChange={(value) => form.setFieldValue('description', value || '')}
          maxLength={1024}
        />
      </Item>

      <Divider titlePlacement="start" style={{ marginTop: '50px' }}>
        {CONNECTOR_NAMES[connector]} Configuration
      </Divider>

      {/* Dynamic connector-specific configuration */}
      <ConfigurationSection form={form} mode={mode} connection={connection} />
    </>
  )
}
