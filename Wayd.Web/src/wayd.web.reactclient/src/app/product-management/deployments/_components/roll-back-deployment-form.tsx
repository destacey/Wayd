'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  DeploymentDto,
  RollBackDeploymentRequest,
} from '@/src/services/wayd-api'
import { useRollBackDeploymentMutation } from '@/src/store/features/product-management/deployments-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form
const { TextArea } = Input

export interface RollBackDeploymentFormProps {
  deployment: DeploymentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RollBackDeploymentFormValues {
  rolledBackAt?: Dayjs
  reason?: string
}

/**
 * Records that a deployment reached its environment and was then reverted.
 *
 * Only a succeeded deployment can be rolled back — reverting something that never arrived is not a
 * rollback. A rollback still counts as having reached production for deployment frequency, and counts
 * against change failure rate, which is what separates the two measures.
 *
 * The revert cannot predate the completion, which is what the aggregate enforces.
 */
const RollBackDeploymentForm = ({
  deployment,
  onFormComplete,
  onFormCancel,
}: RollBackDeploymentFormProps) => {
  const messageApi = useMessage()

  const [rollBackDeployment] = useRollBackDeploymentMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RollBackDeploymentFormValues>({
      onSubmit: async (values: RollBackDeploymentFormValues, form) => {
        try {
          const request = {
            reason: values.reason,
            rolledBackAt: values.rolledBackAt?.toDate(),
          } as RollBackDeploymentRequest

          const response = await rollBackDeployment({
            id: deployment.id,
            cacheKey: deployment.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Deployment rolled back.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while rolling back the deployment. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while rolling back the deployment. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  // A rollback follows the completion, which a succeeded deployment always has.
  const earliest = dayjs(deployment.completedAt ?? deployment.startedAt)

  return (
    <Modal
      title="Roll Back Deployment"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Roll Back"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="roll-back-deployment-form"
      >
        <Item
          label="Rolled Back At"
          name="rolledBackAt"
          // Stated as a rule as well as disabled dates: `disabledDate` works a day at a time, so a
          // same-day time before the completion would otherwise pass to an aggregate that refuses it.
          rules={[
            {
              validator: (_, value: Dayjs | undefined) =>
                !value || !value.isBefore(earliest)
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('A rollback cannot precede the deployment completing'),
                    ),
            },
          ]}
          extra="Leave empty to record it as now."
        >
          <DatePicker
            showTime
            style={{ width: '100%' }}
            disabledDate={(current) => current.isBefore(earliest, 'day')}
          />
        </Item>
        <Item
          label="Reason"
          name="reason"
          rules={[{ max: 1024, message: 'Reason cannot be longer than 1024 characters' }]}
          extra="Recorded in the deployment's status history."
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default RollBackDeploymentForm
