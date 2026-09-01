'use client'

import { useModalForm } from '@/src/hooks'
import { Form, Modal, Select, Spin } from 'antd'
import { useEffect } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { TagAssignment, TagCategory } from './types'

/** One field per category, keyed by category id. */
type ManageTagsFormValues = Record<string, string | string[] | undefined>

export interface TagChanges {
  /** Tag ids to apply. */
  added: string[]
  /** Tag ids to remove. */
  removed: string[]
}

export interface ManageTagsFormProps {
  /** The axes to offer. Callers pass the active ones. */
  categories: TagCategory[]
  /** What the record currently carries. */
  tags: TagAssignment[]
  categoriesLoading?: boolean
  /**
   * Applies the change. Returns true to close, false to keep the form open —
   * the caller owns how tags are saved and how a failure is reported, because
   * that differs per area.
   */
  onSave: (changes: TagChanges) => Promise<boolean>
  onFormComplete: () => void
  onFormCancel: () => void
  title?: string
  /** Permission the modal gates on, checked by useModalForm. */
  permission: string
}

const toTagIds = (value: string | string[] | undefined): string[] => {
  if (Array.isArray(value)) return value
  return value ? [value] : []
}

const tagIdsByCategory = (tags: TagAssignment[]) => {
  const grouped = new Map<string, string[]>()
  for (const tag of tags) {
    grouped.set(tag.categoryId, [...(grouped.get(tag.categoryId) ?? []), tag.tagId])
  }
  return grouped
}

/**
 * Tag options for a category: the active ones, plus any inactive tag the record
 * already carries. Dropping the latter would hide it while it was still attached,
 * and since the diff runs against what was carried, the next save would remove it
 * unasked.
 *
 * Alphabetical, because a tag's position on its axis carries no meaning and a
 * picker is scanned for a label the reader already has in mind. The API returns
 * them unordered, so the sort belongs here.
 */
const optionsFor = (category: TagCategory, carried: string[]) =>
  category.tags
    .filter((tag) => tag.isActive || carried.includes(tag.id))
    .slice()
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .map((tag) => ({
      value: tag.id,
      label: tag.isActive ? tag.name : `${tag.name} (inactive)`,
    }))

/**
 * Applies a record's tags across every offered axis at once.
 *
 * Holds no knowledge of what is being tagged: categories, current tags and the
 * save all arrive as props, so any area with curated tags can use it.
 *
 * A category the caller does not pass is left alone — its tags never enter the
 * diff. That is what keeps a tag on a deactivated axis from being stripped by a
 * save that never offered it.
 */
const ManageTagsForm = ({
  categories,
  tags,
  categoriesLoading = false,
  onSave,
  onFormComplete,
  onFormCancel,
  title = 'Manage Tags',
  permission,
}: ManageTagsFormProps) => {
  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ManageTagsFormValues>({
      onSubmit: async (values: ManageTagsFormValues) => {
        const current = tagIdsByCategory(tags)

        const added: string[] = []
        const removed: string[] = []
        for (const category of categories) {
          const before = current.get(category.id) ?? []
          const after = toTagIds(values[category.id])
          added.push(...after.filter((id) => !before.includes(id)))
          removed.push(...before.filter((id) => !after.includes(id)))
        }

        return await onSave({ added, removed })
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while updating the tags. Please try again.',
      permission,
    })

  useEffect(() => {
    const current = tagIdsByCategory(tags)
    const values: ManageTagsFormValues = {}
    for (const category of categories) {
      const tagIds = current.get(category.id) ?? []
      values[category.id] = category.allowsMany ? tagIds : tagIds[0]
    }
    form.setFieldsValue(values)
  }, [tags, categories, form])

  const current = tagIdsByCategory(tags)

  return (
    <Modal
      title={title}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Spin spinning={categoriesLoading}>
        <Form form={form} size="small" layout="vertical" name="manage-tags-form">
          {categories.map((category) => (
            <Form.Item
              key={category.id}
              name={category.id}
              label={category.name}
              extra={category.description}
            >
              <Select
                mode={category.allowsMany ? 'multiple' : undefined}
                allowClear
                options={optionsFor(category, current.get(category.id) ?? [])}
                placeholder={`Select ${category.name}`}
                optionFilterProp="label"
              />
            </Form.Item>
          ))}
        </Form>
      </Spin>
    </Modal>
  )
}

export default ManageTagsForm
