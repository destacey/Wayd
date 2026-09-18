'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  EndProductDependencyRequest,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { useEndProductDependencyMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'

const { Item } = Form

export interface EndProductDependencyFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EndProductDependencyFormValues {
  endsOn?: Dayjs
}

/**
 * Records that a product stopped depending on another. The dependency is kept and still counts for the period
 * it held, which is the difference from removing it.
 */
const EndProductDependencyForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: EndProductDependencyFormProps) => {
  const messageApi = useMessage()

  const [endProductDependency] = useEndProductDependencyMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EndProductDependencyFormValues>({
      onSubmit: async (values: EndProductDependencyFormValues, form) => {
        try {
          const request = {
            endsOn: values.endsOn?.format('YYYY-MM-DD'),
          } as EndProductDependencyRequest

          const response = await endProductDependency({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency ended.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while ending the dependency. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while ending the dependency. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  const startedOn = dayjs(dependency.startsOn)

  return (
    <Modal
      title="End Dependency"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="End"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        title={`${dependency.product.name} no longer depends on ${dependency.dependsOnProduct.name}`}
        description="The dependency is kept and still counts for the period it held. If it was never true, remove it instead."
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="end-product-dependency-form"
      >
        <Item
          label="Last Day"
          name="endsOn"
          extra="The last day the dependency held — the day it started, at the earliest. Leave empty for today."
        >
          <DatePicker
            style={{ width: '100%' }}
            disabledDate={(current) =>
              current.isBefore(startedOn, 'day') ||
              current.isAfter(dayjs(), 'day')
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EndProductDependencyForm
