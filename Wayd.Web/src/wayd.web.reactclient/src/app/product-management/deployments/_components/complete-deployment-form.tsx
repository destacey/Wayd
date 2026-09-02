'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  DeploymentDto,
  FailDeploymentRequest,
  SucceedDeploymentRequest,
} from '@/src/services/wayd-api'
import {
  useFailDeploymentMutation,
  useSucceedDeploymentMutation,
} from '@/src/store/features/product-management/deployments-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form
const { TextArea } = Input

/** Which terminal outcome is being recorded. */
export type DeploymentOutcome = 'Succeeded' | 'Failed'

export interface CompleteDeploymentFormProps {
  deployment: DeploymentDto
  outcome: DeploymentOutcome
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CompleteDeploymentFormValues {
  completedAt?: Dayjs
  reason?: string
}

/**
 * Records how a deployment finished.
 *
 * Succeeding and failing are the same act with different meanings, so they share a form: both close
 * the deployment at a moment, and only the reason differs — a success needs none, a failure usually
 * has one.
 *
 * The completion cannot predate the start, which is what the aggregate enforces.
 */
const CompleteDeploymentForm = ({
  deployment,
  outcome,
  onFormComplete,
  onFormCancel,
}: CompleteDeploymentFormProps) => {
  const messageApi = useMessage()

  const [succeedDeployment] = useSucceedDeploymentMutation()
  const [failDeployment] = useFailDeploymentMutation()

  const isFailure = outcome === 'Failed'
  const gerund = isFailure ? 'failing' : 'succeeding'

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CompleteDeploymentFormValues>({
      onSubmit: async (values: CompleteDeploymentFormValues, form) => {
        try {
          const completedAt = values.completedAt?.toDate()

          const response = isFailure
            ? await failDeployment({
                id: deployment.id,
                cacheKey: deployment.key,
                request: {
                  reason: values.reason,
                  completedAt,
                } as FailDeploymentRequest,
              })
            : await succeedDeployment({
                id: deployment.id,
                cacheKey: deployment.key,
                request: { completedAt } as SucceedDeploymentRequest,
              })

          if (response.error) throw response.error

          messageApi.success(
            isFailure
              ? 'Deployment recorded as failed.'
              : 'Deployment recorded as succeeded.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An error occurred while ${gerund} the deployment. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${gerund} the deployment. Please try again.`,
      permission: 'Permissions.Delivery.Update',
    })

  const startedAt = dayjs(deployment.startedAt)

  return (
    <Modal
      title={isFailure ? 'Fail Deployment' : 'Succeed Deployment'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: isFailure }}
      okText={isFailure ? 'Record Failure' : 'Record Success'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="complete-deployment-form"
      >
        <Item
          label="Completed At"
          name="completedAt"
          // Stated as a rule, not only as disabled dates on the picker: the aggregate refuses a
          // completion before the start, and `disabledDate` works a day at a time, so a same-day
          // time earlier than the start would otherwise pass. It also registers the field with the
          // form — every field here is optional, and with no rule anywhere antd reports the form as
          // untouched and leaves the OK button disabled with nothing to correct.
          rules={[
            {
              validator: (_, value: Dayjs | undefined) =>
                !value || !value.isBefore(startedAt)
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('A deployment cannot finish before it began'),
                    ),
            },
          ]}
          extra="Leave empty to record it as completing now."
        >
          <DatePicker
            showTime
            style={{ width: '100%' }}
            disabledDate={(current) => current.isBefore(startedAt, 'day')}
          />
        </Item>
        {isFailure && (
          <Item
            label="Reason"
            name="reason"
            rules={[{ max: 1024, message: 'Reason cannot be longer than 1024 characters' }]}
            extra="Recorded in the deployment's status history."
          >
            <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
          </Item>
        )}
      </Form>
    </Modal>
  )
}

export default CompleteDeploymentForm
