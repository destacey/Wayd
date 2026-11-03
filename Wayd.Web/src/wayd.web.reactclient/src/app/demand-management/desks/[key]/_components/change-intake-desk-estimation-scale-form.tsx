'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeDeskEstimationScaleMutation,
  useGetIntakeEstimationScaleChoicesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Flex, Form, Modal, Select, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph, Text } = Typography

export interface ChangeIntakeDeskEstimationScaleFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeEstimationScaleFormValues {
  estimationScaleId?: string
}

/**
 * Chooses the estimation scale a desk's team sizes its requests on, or stops estimating them. Offered where
 * the desk's `canManage` says so: to its Desk Admins and the Demand Management Admin.
 */
const ChangeIntakeDeskEstimationScaleForm: FC<
  ChangeIntakeDeskEstimationScaleFormProps
> = ({ desk, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const [changeEstimationScale] = useChangeIntakeDeskEstimationScaleMutation()
  const { data: choices, isLoading } = useGetIntakeEstimationScaleChoicesQuery()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeEstimationScaleFormValues>({
      onSubmit: async (values, form) => {
        try {
          await changeEstimationScale({
            deskId: desk.id,
            estimationScaleId: values.estimationScaleId,
          }).unwrap()
          messageApi.success('Estimation scale changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to change the estimation scale.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the estimation scale.',
    })

  // A deactivated scale stays pinned until changed, but is no longer a choice; keep it showing by name.
  const current = desk.estimationScale
  const options = [
    ...(current && !(choices ?? []).some((c) => c.id === current.id)
      ? [
          {
            value: current.id,
            label: `${current.name} (Inactive)`,
            values: undefined,
          },
        ]
      : []),
    ...(choices ?? []).map((s) => ({
      value: s.id,
      label: s.name,
      values: s.estimateValues.join(', '),
    })),
  ]

  return (
    <Modal
      title="Change Estimation Scale"
      open={isOpen}
      onOk={handleOk}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        What {desk.name}&apos;s team sizes its requests on. The desk uses the
        scale&apos;s current version, and estimates already given keep the
        version they were given on. The scale can change only while no open
        request has an estimate on it. Scales are built in Settings, under
        Estimation Scales.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-desk-estimation-scale-form"
        initialValues={{ estimationScaleId: current?.id }}
      >
        <Item name="estimationScaleId" label="Estimation Scale">
          <Select
            allowClear
            placeholder="Do not estimate requests"
            loading={isLoading}
            options={options}
            // Each scale's values under its name, so a scale is chosen by what it sizes with.
            optionRender={(option) => (
              <Flex vertical>
                <Text>{option.data.label}</Text>
                {option.data.values && (
                  <Text type="secondary">{option.data.values}</Text>
                )}
              </Flex>
            )}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeIntakeDeskEstimationScaleForm
