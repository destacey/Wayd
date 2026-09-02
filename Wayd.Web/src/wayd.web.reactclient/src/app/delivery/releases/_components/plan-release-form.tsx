'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  CutReleaseRequest,
  MarkReleaseReleasedRequest,
  PlanReleaseRequest,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  useCutReleaseMutation,
  useMarkReleaseReleasedMutation,
  usePlanReleaseMutation,
} from '@/src/store/features/delivery/releases-api'
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
  productId: string
  version: string
  name?: string
  targetDate?: Dayjs
  cutDate?: Dayjs
  releasedDate?: Dayjs
}

/**
 * Records a release against a releasable product.
 *
 * Not every release is planned before it ships: one entered after the fact carries the dates it
 * already has. Supplying them walks the same endpoints a person would use later, so the status and
 * the history land as though the steps were taken in order.
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
  const [cutRelease] = useCutReleaseMutation()
  const [markReleased] = useMarkReleaseReleasedMutation()
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

          const { id, key } = response.data!

          // Recorded as separate steps because that is what they are: each is the same endpoint a
          // person would reach for later, and the aggregate applies its own guards to each. A
          // release created after the fact is not a different kind of release, only one whose steps
          // are being entered at once.
          //
          // The release exists from here on, so a failure below is reported against it by key rather
          // than as a failure to create — retrying the whole form would make a second release.
          if (values.cutDate) {
            const cut = await cutRelease({
              id,
              cacheKey: key,
              request: { id, cutDate: values.cutDate.format('YYYY-MM-DD') } as unknown as CutReleaseRequest,
            })
            if (cut.error) {
              messageApi.error(
                `Release ${key} was created, but recording the cut date failed. Set it from the release.`,
              )
              return true
            }
          }

          if (values.releasedDate) {
            const released = await markReleased({
              id,
              cacheKey: key,
              request: {
                id,
                releasedDate: values.releasedDate.format('YYYY-MM-DD'),
              } as unknown as MarkReleaseReleasedRequest,
            })
            if (released.error) {
              messageApi.error(
                `Release ${key} was created, but recording the released date failed. Set it from the release.`,
              )
              return true
            }
          }

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

  const cutDate = Form.useWatch('cutDate', form)

  const productOptions = (products ?? [])
    .filter((product) => product.isReleasable)
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
          rules={[{ required: true, message: 'Product is required' }]}
          extra="Only products that can be released are listed. A product's type decides this."
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
          extra="For example 4.8.2, 2026.04, or v3-beta."
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
          label="Cut Date"
          name="cutDate"
          extra="Leave empty if this release has not been cut yet."
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
        <Item
          label="Released Date"
          name="releasedDate"
          extra="Leave empty if this release has not shipped yet."
        >
          <DatePicker
            style={{ width: '100%' }}
            // The aggregate refuses a released date before the cut date.
            disabledDate={
              cutDate ? (current) => current.isBefore(cutDate, 'day') : undefined
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default PlanReleaseForm
