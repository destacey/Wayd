'use client'

import { Alert, Flex, Form, Input, Modal, Spin, Tag, Typography } from 'antd'
import { CSSProperties, useEffect } from 'react'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import useTheme from '@/src/components/contexts/theme'
import {
  UpdatePokerSessionRequest,
  useGetPokerSessionQuery,
  useUpdatePokerSessionMutation,
} from '@/src/store/features/planning/poker-sessions-api'
import { useGetEstimationScalesQuery } from '@/src/store/features/planning/estimation-scales-api'
import { useModalForm } from '@/src/hooks'
import createStyles from '../../_components/poker-sessions.module.css'

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

export interface EditPokerSessionFormProps {
  sessionKey: number
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditPokerSessionFormValues {
  name: string
  estimationScaleId: number
}

interface EstimationScaleCardProps {
  scale: EstimationScaleDto
  selected: boolean
  disabled: boolean
  onSelect: (id: number) => void
}

const EstimationScaleCard = ({
  scale,
  selected,
  disabled,
  onSelect,
}: EstimationScaleCardProps) => {
  const cardClass = selected
    ? `${createStyles.scaleCard} ${createStyles.scaleCardSelected}`
    : createStyles.scaleCard

  return (
    <div
      onClick={() => !disabled && onSelect(scale.id)}
      className={cardClass}
      style={disabled ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}
    >
      <Text strong className={createStyles.scaleCardName}>
        {scale.name}
      </Text>
      {scale.description && (
        <Text type="secondary" className={createStyles.scaleCardDescription}>
          {scale.description}
        </Text>
      )}
      <Flex wrap gap={4} className={createStyles.scaleCardValues}>
        {scale.values.map((v) => (
          <Tag key={v} style={{ margin: 0 }}>
            {v}
          </Tag>
        ))}
      </Flex>
    </div>
  )
}

const EditPokerSessionForm = ({
  sessionKey,
  onFormUpdate,
  onFormCancel,
}: EditPokerSessionFormProps) => {
  const { token } = useTheme()
  const messageApi = useMessage()

  const { data: session, isLoading: sessionLoading } = useGetPokerSessionQuery(
    sessionKey.toString(),
  )
  const [updatePokerSession] = useUpdatePokerSessionMutation()
  const { data: estimationScales, isLoading: scalesLoading } =
    useGetEstimationScalesQuery()

  const hasRounds = (session?.rounds?.length ?? 0) > 0

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditPokerSessionFormValues>({
      onSubmit: async (values: EditPokerSessionFormValues, form) => {
          if (!session) return false
          try {
            const request: UpdatePokerSessionRequest = {
              name: values.name,
              estimationScaleId: values.estimationScaleId,
            }
            const response = await updatePokerSession({
              id: session.id,
              key: session.key,
              request,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully updated poker session.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An error occurred while updating the poker session. Please try again.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the poker session. Please try again.',
      permission: 'Permissions.PokerSessions.Update',
    })

  useEffect(() => {
    if (isOpen && session) {
      form.setFieldsValue({
        name: session.name,
        estimationScaleId: session.estimationScale?.id,
      })
    }
  }, [isOpen, session, form])

  const activeScales = estimationScales ?? []
  const selectedScaleId = Form.useWatch('estimationScaleId', form)

  const handleScaleSelect = (id: number) => {
    if (!hasRounds) {
      form.setFieldsValue({ estimationScaleId: id })
    }
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
      title="Edit Poker Session"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || sessionLoading || !session }}
      okText="Save"
      loading={sessionLoading}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
          form={form}
          size="small"
          layout="vertical"
          name="edit-poker-session-form"
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
          <div className={createStyles.scaleLabel}>
            <span className={createStyles.scaleRequired}>*</span> Estimation
            Scale
          </div>
          {hasRounds && (
            <Alert
              message="Scale cannot be changed after rounds have started."
              type="info"
              showIcon
              style={{ marginBottom: 8, fontSize: 12 }}
            />
          )}
          {scalesLoading ? (
            <Flex justify="center" className={createStyles.scaleCardLoading}>
              <Spin size="small" />
            </Flex>
          ) : (
            <Flex vertical gap={8} style={cssVars}>
              {activeScales.map((scale) => (
                <EstimationScaleCard
                  key={scale.id}
                  scale={scale}
                  selected={selectedScaleId === scale.id}
                  disabled={hasRounds}
                  onSelect={handleScaleSelect}
                />
              ))}
            </Flex>
          )}
        </Form>
    </Modal>
  )
}

export default EditPokerSessionForm
