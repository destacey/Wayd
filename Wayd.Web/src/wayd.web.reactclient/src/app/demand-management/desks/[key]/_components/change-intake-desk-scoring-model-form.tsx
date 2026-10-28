'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeDeskScoringModelMutation,
  useGetIntakeScoringModelChoicesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Select, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface ChangeIntakeDeskScoringModelFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeScoringModelFormValues {
  scoringModelId?: string
}

/**
 * Chooses the scoring model a desk's agents score its requests with, or stops scoring them. Offered where
 * the desk's `canManage` says so: to its Desk Admins and the Demand Management Admin.
 */
const ChangeIntakeDeskScoringModelForm: FC<
  ChangeIntakeDeskScoringModelFormProps
> = ({ desk, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const [changeScoringModel] = useChangeIntakeDeskScoringModelMutation()
  const { data: choices, isLoading } = useGetIntakeScoringModelChoicesQuery()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeScoringModelFormValues>({
      onSubmit: async (values, form) => {
        try {
          await changeScoringModel({
            deskId: desk.id,
            scoringModelId: values.scoringModelId,
          }).unwrap()
          messageApi.success('Scoring model changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to change the scoring model.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the scoring model.',
    })

  // An archived model stays assigned until changed, but is no longer a choice; keep it showing by name.
  const options = [
    ...(desk.scoringModel &&
    !(choices ?? []).some((c) => c.id === desk.scoringModel?.id)
      ? [
          {
            value: desk.scoringModel.id,
            label: `${desk.scoringModel.name} (Archived)`,
          },
        ]
      : []),
    ...(choices ?? []).map((m) => ({ value: m.id, label: m.name })),
  ]

  return (
    <Modal
      title="Change Scoring Model"
      open={isOpen}
      onOk={handleOk}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        What {desk.name}&apos;s agents rate its requests on, and what the
        requests list ranks them by. Scores already recorded keep the model they
        were scored with, and only scores from the desk&apos;s current model
        rank together. Models are built in Settings, under Scoring Models.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-desk-scoring-model-form"
        initialValues={{ scoringModelId: desk.scoringModel?.id }}
      >
        <Item name="scoringModelId" label="Scoring Model">
          <Select
            allowClear
            placeholder="Do not score requests"
            loading={isLoading}
            options={options}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeIntakeDeskScoringModelForm
