'use client'

import { Modal, Alert, Typography, Button, Flex } from 'antd'
import { CopyOutlined, ExclamationCircleOutlined } from '@ant-design/icons'
import { useMessage } from '@/src/components/contexts/messaging'

const { Text, Paragraph } = Typography

export interface PersonalAccessTokenCreatedModalProps {
  token: string | null
  onClose: () => void
}

const PersonalAccessTokenCreatedModal = ({
  token,
  onClose,
}: PersonalAccessTokenCreatedModalProps) => {
  const messageApi = useMessage()

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
    messageApi.success('Token copied to clipboard')
  }

  return (
    <Modal
      title="Token Created Successfully"
      open={!!token}
      onCancel={onClose}
      footer={[
        <Button
          key="copy"
          type="primary"
          icon={<CopyOutlined />}
          onClick={() => copyToClipboard(token!)}
        >
          Copy Token
        </Button>,
        <Button key="close" onClick={onClose}>
          Close
        </Button>,
      ]}
      closable={false}
    >
      <Alert
        title="Save This Token Now!"
        description="This is the only time you will see this token. Copy it and store it securely."
        type="error"
        showIcon
        icon={<ExclamationCircleOutlined />}
        style={{ marginBottom: 16 }}
      />
      <Flex vertical gap={12}>
        <Text strong>Your personal access token:</Text>
        <Paragraph
          code
          copyable
          style={{
            padding: 8,
            borderRadius: 4,
            wordBreak: 'break-all',
          }}
        >
          {token}
        </Paragraph>
        <Text type="secondary">
          Use this token in the <code>x-api-key</code> header when making API
          requests.
        </Text>
      </Flex>
    </Modal>
  )
}

export default PersonalAccessTokenCreatedModal
