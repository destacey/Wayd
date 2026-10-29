'use client'

import { tagText } from '@/src/components/common/tags'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import {
  useMergeProductTagMutation,
  useSearchProductTagsQuery,
} from '@/src/store/features/product-management/product-tags-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Modal, Select } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface MergeProductTagFormProps {
  tag: ProductTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MergeProductTagFormValues {
  targetTagId: string
}

/**
 * Moves a free tag's products onto another tag and deletes the free tag.
 *
 * Merging into a tag on a category is how a free tag that caught on is
 * promoted: add the tag to its category first, then merge into it.
 */
const MergeProductTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: MergeProductTagFormProps) => {
  const messageApi = useMessage()

  const [mergeProductTag] = useMergeProductTagMutation()
  // Every tag that can be applied — free tags and tags on active categories.
  const { data: tags, isLoading } = useSearchProductTagsQuery()

  const targets = (tags ?? [])
    .filter((candidate) => candidate.id !== tag.id)
    .map((candidate) => ({
      value: candidate.id,
      label: tagText(candidate),
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MergeProductTagFormValues>({
      onSubmit: async (values) => {
        try {
          const response = await mergeProductTag({
            id: tag.id,
            targetTagId: values.targetTagId,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Tags merged successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(
            apiError.detail ??
              'An error occurred while merging the tags. Please try again.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while merging the tags. Please try again.',
      permission: 'Permissions.ProductTagCategories.Update',
    })

  return (
    <Modal
      title="Merge Free Tag"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Merge"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="merge-product-tag-form"
      >
        <Item
          label="Merge into"
          name="targetTagId"
          rules={[{ required: true, message: 'Choose the tag to merge into' }]}
          extra={`The ${tag.productCount} product(s) carrying "${tag.name}" will carry this tag instead, and "${tag.name}" will be deleted.`}
        >
          <Select
            options={targets}
            loading={isLoading}
            placeholder="Select a tag"
            showSearch={{ optionFilterProp: 'label' }}
          />
        </Item>
      </Form>
      <Alert
        type="info"
        showIcon
        title="On a category that allows one tag, a product's existing tag there is replaced."
      />
    </Modal>
  )
}

export default MergeProductTagForm
