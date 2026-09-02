'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { PlanReleaseRequest } from '@/src/services/wayd-api'
import { usePlanReleaseMutation } from '@/src/store/features/product-management/releases-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal, Select } from 'antd'
import { Dayjs } from 'dayjs'

const { Item } = Form

export interface PlanReleaseFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
  /** Pre-selects the product, for planning from that product's page. */
  defaultProductId?: string
}

interface PlanReleaseFormValues {
  productId?: string
  version: string
  name?: string
  targetDate?: Dayjs
}

/**
 * Drafts an announcement.
 *
 * Contents are not here. An announcement is commonly drafted before anyone knows which versions will
 * make it, so the release starts empty and gathers its contents later.
 *
 * Every product is offered, not only the releasable ones. That gate asks whether an artifact can be
 * cut against a node, which is a version's question — a release usually sits under a product line,
 * which is typically not releasable.
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
          } as unknown as PlanReleaseRequest

          const response = await planRelease(request)
          if (response.error) throw response.error

          const { key } = response.data!

          messageApi.success(`Release created successfully. Release key: ${key}`)
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
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Add Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
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
          extra="Leave empty for a release spanning product lines. A release with no product is left out when releases are filtered by product."
        >
          <Select
            options={productOptions}
            loading={isLoading}
            placeholder="Select a product"
            allowClear
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
          extra="The release's own label — 2026.07, Spring Release, R4. Not the version number of anything inside it."
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
        <Item
          label="Target Date"
          name="targetDate"
          extra="When the release is expected to be announced."
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default PlanReleaseForm
