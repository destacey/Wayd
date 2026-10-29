'use client'

import { compareTags, tagText } from '@/src/components/common/tags'
import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestTagListItemDto } from '@/src/services/wayd-api'
import {
  useMergeIntakeRequestTagMutation,
  useSearchIntakeRequestTagsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Form, Modal, Select, Typography } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface MergeFreeTagFormProps {
  tag: IntakeRequestTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MergeFreeTagFormValues {
  targetTagId: string
}

/**
 * Moves a free tag's requests to another tag and deletes it — to fold
 * near-duplicates together, or to promote a free tag onto an axis once the tag
 * has been added there.
 */
const MergeFreeTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: MergeFreeTagFormProps) => {
  const messageApi = useMessage()

  const [mergeTag] = useMergeIntakeRequestTagMutation()
  // Active tags only, on axes and free: the API refuses an inactive target.
  const { data: tags, isLoading } = useSearchIntakeRequestTagsQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MergeFreeTagFormValues>({
      onSubmit: async (values) => {
        try {
          await mergeTag({
            id: tag.id,
            targetTagId: values.targetTagId,
          }).unwrap()
          messageApi.success('Tags merged.')
          return true
        } catch (error) {
          messageApi.error(
            toApiError(error).detail ??
              'An error occurred while merging the tags. Please try again.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while merging the tags. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  const options = (tags ?? [])
    .filter((t) => t.id !== tag.id)
    .slice()
    .sort(compareTags)
    .map((t) => ({ value: t.id, label: tagText(t) }))

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
      <Typography.Paragraph>
        Every request carrying{' '}
        <Typography.Text strong>{tag.name}</Typography.Text> will carry the tag
        you choose instead, and{' '}
        <Typography.Text strong>{tag.name}</Typography.Text> will be deleted. On
        an axis that takes one tag, a request&apos;s existing tag there is
        replaced.
      </Typography.Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="merge-free-intake-request-tag-form"
      >
        <Item
          label="Merge into"
          name="targetTagId"
          rules={[{ required: true, message: 'Choose a tag to merge into' }]}
          extra="Another free tag, or a tag on an axis. To promote this tag onto an axis, add it to the axis first."
        >
          <Select
            loading={isLoading}
            options={options}
            placeholder="Select a tag"
            showSearch={{ optionFilterProp: 'label' }}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default MergeFreeTagForm
