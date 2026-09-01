'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { PlanReleaseRequest } from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { usePlanReleaseMutation } from '@/src/store/features/delivery/releases-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd'
import { Dayjs } from 'dayjs'

const { Item } = Form

export interface PlanReleaseFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
  /** Pre-selects the product, for planning from that product's page. */
  defaultProductId?: string
}

interface PlanReleaseFormValues {
  productId: string
  version: string
  name?: string
  targetDate?: Dayjs
  sequence?: number
}

/**
 * Plans a release against a releasable product.
 *
 * Only releasable products are offered. The API refuses the rest — a release has to be a cut of
 * something that ships — and a picker listing every product would make that a failed submit rather
 * than an unavailable choice.
 */
const PlanReleaseForm = ({
  onFormComplete,
  onFormCancel,
  defaultProductId,
}: PlanReleaseFormProps) => {
  const messageApi = useMessage()

  const [planRelease] = usePlanReleaseMutation()
  const { data: products, isLoading } = useGetProductsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<PlanReleaseFormValues>({
      onSubmit: async (values: PlanReleaseFormValues, form) => {
        try {
          const request = {
            productId: values.productId,
            version: values.version,
            name: values.name,
            targetDate: values.targetDate?.format('YYYY-MM-DD'),
            sequence: values.sequence,
          } as unknown as PlanReleaseRequest

          const response = await planRelease(request)
          if (response.error) throw response.error

          messageApi.success(
            `Release planned successfully. Release key: ${response.data!.key}`,
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
                'An error occurred while planning the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while planning the release. Please try again.',
      permission: 'Permissions.Releases.Create',
    })

  const productOptions = (products ?? [])
    .filter((product) => product.isReleasable)
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Plan Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Plan"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="plan-release-form"
        initialValues={defaultProductId ? { productId: defaultProductId } : undefined}
      >
        <Item
          label="Product"
          name="productId"
          rules={[{ required: true, message: 'Product is required' }]}
          extra="Only products whose type can be released are listed."
        >
          <Select
            options={productOptions}
            loading={isLoading}
            placeholder="Select a product"
            showSearch
            optionFilterProp="label"
          />
        </Item>
        <Item
          label="Version"
          name="version"
          rules={[
            { required: true, message: 'Version is required' },
            { max: 64, message: 'Version cannot be longer than 64 characters' },
          ]}
          extra="Free text — Wayd never parses or orders by it."
        >
          <Input />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[{ max: 128, message: 'Name cannot be longer than 128 characters' }]}
        >
          <Input />
        </Item>
        <Item label="Target Date" name="targetDate">
          <DatePicker style={{ width: '100%' }} />
        </Item>
        <Item
          label="Sequence"
          name="sequence"
          extra="Only needed where release order differs from date order, as a backport does."
        >
          <InputNumber style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default PlanReleaseForm
