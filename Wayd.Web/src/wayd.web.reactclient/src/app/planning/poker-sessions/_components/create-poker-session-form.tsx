'use client'

import { Flex, Form, Input, Modal, Tag, Typography } from 'antd'
import { CSSProperties } from 'react'
import {
  CreatePokerSessionRequest,
  EstimationScaleDto,
} from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import useTheme from '@/src/components/contexts/theme'
import { useCreatePokerSessionMutation } from '@/src/store/features/planning/poker-sessions-api'
import { useGetEstimationScalesQuery } from '@/src/store/features/planning/estimation-scales-api'
import { useModalForm } from '@/src/hooks'
import styles from './poker-sessions.module.css'

const { Item } = Form
const { TextArea } = Input
const { Text } = Typography

interface ScaleCssVars extends CSSProperties {
  '--scale-border': string
  '--scale-bg': string
  '--scale-primary': string
  '--scale-primary-bg': string
  '--scale-radius-lg': string
}

export interface CreatePokerSessionFormProps {
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreatePokerSessionFormValues {
  name: string
  estimationScaleId: number
}

interface EstimationScaleCardProps {
  scale: EstimationScaleDto
  selected: boolean
  onSelect: (id: number) => void
}

const EstimationScaleCard = ({
  scale,
  selected,
  onSelect,
}: EstimationScaleCardProps) => {
  const cardClass = selected
    ? `${styles.scaleCard} ${styles.scaleCardSelected}`
    : styles.scaleCard

  return (
    <div onClick={() => onSelect(scale.id)} className={cardClass}>
      <Text strong className={styles.scaleCardName}>
        {scale.name}
      </Text>
      {scale.description && (
        <Text type="secondary" className={styles.scaleCardDescription}>
          {scale.description}
        </Text>
      )}
      <Flex wrap gap={4} className={styles.scaleCardValues}>
        {scale.values.map((v) => (
          <Tag key={v} style={{ margin: 0 }}>
            {v}
          </Tag>
        ))}
      </Flex>
    </div>
  )
}

const CreatePokerSessionForm = ({
  onFormCreate,
  onFormCancel,
}: CreatePokerSessionFormProps) => {
  const { token } = useTheme()
  const messageApi = useMessage()

  const [createPokerSession] = useCreatePokerSessionMutation()
  const { data: estimationScales, isLoading: scalesLoading } =
    useGetEstimationScalesQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreatePokerSessionFormValues>({
      onSubmit: async (values: CreatePokerSessionFormValues, form) => {
          try {
            const request: CreatePokerSessionRequest = {
              name: values.name,
              estimationScaleId: values.estimationScaleId,
            }
            const response = await createPokerSession(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully created poker session.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An error occurred while creating the poker session. Please try again.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the poker session. Please try again.',
      permission: 'Permissions.PokerSessions.Create',
    })

  const activeScales = estimationScales ?? []

  const selectedScaleId = Form.useWatch('estimationScaleId', form)

  const handleScaleSelect = (id: number) => {
    form.setFieldsValue({ estimationScaleId: id })
  }

  const cssVars: ScaleCssVars = {
    '--scale-border': token.colorBorderSecondary,
    '--scale-bg': token.colorBgContainer,
    '--scale-primary': token.colorPrimary,
    '--scale-primary-bg': token.colorPrimaryBg,
    '--scale-radius-lg': `${token.borderRadiusLG}px`,
  }

  return (
    <Modal
      title="Create Poker Session"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      loading={scalesLoading}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-poker-session-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={256}
          />
        </Item>
        <Item
          name="estimationScaleId"
          noStyle
          rules={[
            { required: true, message: 'Please select an estimation scale' },
          ]}
        >
          <Input style={{ display: 'none' }} />
        </Item>
        <div className={styles.scaleLabel}>
          <span className={styles.scaleRequired}>*</span> Estimation Scale
        </div>
        <Flex vertical gap={8} style={cssVars}>
          {activeScales.map((scale) => (
            <EstimationScaleCard
              key={scale.id}
              scale={scale}
              selected={selectedScaleId === scale.id}
              onSelect={handleScaleSelect}
            />
          ))}
        </Flex>
      </Form>
    </Modal>
  )
}

export default CreatePokerSessionForm
