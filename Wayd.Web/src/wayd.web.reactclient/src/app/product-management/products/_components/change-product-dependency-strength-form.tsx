'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ChangeProductDependencyStrengthRequest,
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { useChangeProductDependencyStrengthMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { DependencyStrengthRadio } from './dependency-strength'

const { Item } = Form

export interface ChangeProductDependencyStrengthFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProductDependencyStrengthFormValues {
  strength?: DependencyStrength
  changedOn?: Dayjs
}

/**
 * Changes whether a product stops working without one it depends on.
 *
 * Starts on the other strength, since choosing the current one changes nothing. The change ends the
 * dependency and records a new one from the chosen moment, so time before it is still judged by the strength
 * that held then — the form says so, because the ended dependency is kept.
 */
const ChangeProductDependencyStrengthForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: ChangeProductDependencyStrengthFormProps) => {
  const messageApi = useMessage()

  const [changeStrength] = useChangeProductDependencyStrengthMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProductDependencyStrengthFormValues>({
      onSubmit: async (
        values: ChangeProductDependencyStrengthFormValues,
        form,
      ) => {
        try {
          const request = {
            strength: values.strength,
            changedOn: values.changedOn?.format('YYYY-MM-DD'),
          } as ChangeProductDependencyStrengthRequest

          const response = await changeStrength({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency strength changed.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while changing the dependency strength. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the dependency strength. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  // The current dependency ends the day before the change, so the change can fall no earlier than the day after
  // it started.
  const earliest = dayjs(dependency.startsOn).add(1, 'day')
  const startedToday = earliest.isAfter(dayjs(), 'day')
  const otherStrength =
    dependency.strength === DependencyStrength.Hard
      ? DependencyStrength.Soft
      : DependencyStrength.Hard

  return (
    <Modal
      title="Change Dependency Strength"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || startedToday }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {startedToday ? (
        <Alert
          type="warning"
          showIcon
          title="This dependency started today"
          description="Its strength can change from tomorrow. If it was recorded with the wrong strength, remove it and add it again."
          style={{ marginBottom: 16 }}
        />
      ) : (
        <Alert
          type="info"
          showIcon
          title="The current dependency ends and a new one starts"
          description="The current one ends the day before the change, so time before it is still judged by the strength that held then. It stays in the list under Show ended."
          style={{ marginBottom: 16 }}
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-product-dependency-strength-form"
        initialValues={{ strength: otherStrength }}
      >
        <Item
          name="strength"
          label="Strength"
          rules={[{ required: true, message: 'Choose a strength' }]}
        >
          <DependencyStrengthRadio />
        </Item>
        <Item
          label="First Day"
          name="changedOn"
          extra="The first day the new strength holds. Leave empty for today."
        >
          <DatePicker
            style={{ width: '100%' }}
            disabled={startedToday}
            disabledDate={(current) =>
              current.isBefore(earliest, 'day') ||
              current.isAfter(dayjs(), 'day')
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeProductDependencyStrengthForm
