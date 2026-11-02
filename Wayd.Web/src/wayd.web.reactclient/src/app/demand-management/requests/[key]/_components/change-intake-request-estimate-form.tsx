'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { useChangeIntakeRequestEstimateMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { ConfigProvider, Form, Grid, Modal, Radio, Typography } from 'antd'
import { FC } from 'react'
import { TOUCH_THEME } from '../../../_components/touch-theme'

const { Item } = Form
const { Paragraph, Text } = Typography
const { useBreakpoint } = Grid

/** Stands for "no estimate" among the values, since an option needs a value of its own. */
const NO_ESTIMATE = ''

export interface ChangeIntakeRequestEstimateFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EstimateFormValues {
  value: string
}

/**
 * Sizes the request on its desk's scale, or clears its estimate. Offered only where the server's
 * `canChangeEstimate` says so: to the desk team, while the request is open. The values are the desk's
 * scale version's, in order, so they read from smallest to largest.
 */
const ChangeIntakeRequestEstimateForm: FC<
  ChangeIntakeRequestEstimateFormProps
> = ({ request, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const phone = useBreakpoint().md === false
  const [changeEstimate] = useChangeIntakeRequestEstimateMutation()
  const current = request.substance?.estimate
  // An estimate from another version than the desk's can only be kept or cleared, so it is offered too.
  const foreign =
    current && !request.estimateValues.includes(current.value)
      ? current.value
      : undefined

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<EstimateFormValues>({
      onSubmit: async (values, form) => {
        // Keeping an estimate from another version is no change, and the desk's scale would refuse it.
        if (foreign && values.value === foreign) return true
        try {
          const value = values.value || undefined
          await changeEstimate({
            requestId: request.summary.id,
            value,
          }).unwrap()
          messageApi.success(value ? 'Estimate saved.' : 'Estimate cleared.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to change the estimate.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the estimate.',
    })

  const dialog = (
    <Modal
      title="Change Estimate"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        How big the ask is, on the scale {request.summary.desk.name} estimates
        on. The requester sees the estimate but cannot change it.
      </Paragraph>
      <Form
        form={form}
        size={phone ? undefined : 'small'}
        layout="vertical"
        name="change-intake-request-estimate-form"
        initialValues={{ value: current?.value ?? NO_ESTIMATE }}
      >
        {/* Buttons wrap where a segmented control would scroll, which a long scale needs on a phone. */}
        <Item
          name="value"
          label="Estimate"
          extra={
            foreign ? (
              <Text type="secondary">
                {foreign} was given on {current?.scale.name}, version{' '}
                {current?.scale.versionNumber}, which this desk no longer uses.
                Keep it, clear it, or choose a value on the desk&apos;s scale.
              </Text>
            ) : undefined
          }
        >
          <Radio.Group
            aria-label="Estimate"
            optionType="button"
            buttonStyle="solid"
            options={[
              { value: NO_ESTIMATE, label: 'None' },
              ...(foreign ? [{ value: foreign, label: foreign }] : []),
              ...request.estimateValues.map((v) => ({ value: v, label: v })),
            ]}
          />
        </Item>
      </Form>
    </Modal>
  )

  // On a phone the dialog is tapped, so its controls are touch-sized. Always rendered, with the theme
  // switched, so crossing the breakpoint never remounts the dialog.
  return (
    <ConfigProvider theme={phone ? TOUCH_THEME : undefined}>
      {dialog}
    </ConfigProvider>
  )
}

export default ChangeIntakeRequestEstimateForm
