'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ReleaseDto, UpdateReleaseRequest } from '@/src/services/wayd-api'
import { useUpdateReleaseMutation } from '@/src/store/features/product-management/releases-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'

const { Item } = Form

export interface EditReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditReleaseFormValues {
  productId?: string
  version: string
  name?: string
  notes?: string
}

/**
 * Edits a release's descriptive fields.
 *
 * Offered whatever the release's status, because the domain refuses nothing here: a label or a set of
 * notes can be found wrong long after the announcement, and correcting the wording says nothing about
 * what shipped. The dates and the contents are their own actions, each carrying rules this does not.
 */
const EditReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: EditReleaseFormProps) => {
  const messageApi = useMessage()

  const [updateRelease] = useUpdateReleaseMutation()
  const { data: products, isLoading } = useGetProductsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditReleaseFormValues>({
      onSubmit: async (values: EditReleaseFormValues, form) => {
        try {
          const request = {
            id: release.id,
            productId: values.productId,
            version: values.version,
            name: values.name,
            notes: values.notes,
            // Passed through rather than edited. The update is a whole-record overwrite, so omitting
            // this would clear an ordering an import had set — and there is no way to set one here.
            sequence: release.sequence,
          } as UpdateReleaseRequest

          const response = await updateRelease({
            id: release.id,
            cacheKey: release.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Release updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  const productOptions = (products ?? [])
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Edit Release"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-release-form"
        initialValues={{
          productId: release.product?.id,
          version: release.version,
          name: release.name,
          notes: release.notes,
        }}
      >
        <Item
          label="Product"
          name="productId"
          extra="Clear it for a release spanning product lines. A release with no product is left out when releases are filtered by product."
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
          extra="The release's own label — 2026.07, Spring Release, R4."
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
        <Item label="Notes" name="notes" extra="Written for customers.">
          <MarkdownEditor maxLength={4000} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditReleaseForm
