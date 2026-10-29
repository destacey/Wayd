'use client'

import { useModalForm } from '@/src/hooks'
import { Form, Modal, Select, Spin } from 'antd'
import { useEffect } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { TagAssignment, TagCategory, TagOption } from './types'

/**
 * One field per category, keyed by category id, plus the free tag field. Free
 * tag values mix the ids of existing free tags with names typed in.
 */
type ManageTagsFormValues = Record<string, string | string[] | undefined>

/** The free tag field's name. Not a valid id, so no category can collide with it. */
const FREE_TAGS_FIELD = '__freeTags'

/** The longest tag name the server accepts, counted after whitespace is collapsed. */
const MAX_TAG_NAME_LENGTH = 64

const TAG_NAME_TOO_LONG = `A tag name can be at most ${MAX_TAG_NAME_LENGTH} characters.`

/** A typed name as the server stores it: trimmed, with each run of whitespace collapsed. */
const normalizeName = (raw: string) => raw.trim().replace(/\s+/g, ' ')

export interface TagChanges {
  /** Tag ids to apply. */
  added: string[]
  /** Tag ids to remove. */
  removed: string[]
  /**
   * Names typed into the free tag field that match no free tag offered. The
   * caller applies each by name, which creates the free tag when none has it.
   */
  addedNames: string[]
}

/** The free tag field, offered when the record's area allows free tags. */
export interface FreeTagsField {
  /**
   * The existing free tags to suggest. A typed name matching one of these,
   * ignoring case, applies it rather than proposing a new one.
   */
  options: TagOption[]
  loading?: boolean
}

export interface ManageTagsFormProps {
  /**
   * The axes to offer, each with the tags it may show. Only these are diffed, so
   * a tag on an axis not passed stays on the record.
   */
  categories: TagCategory[]
  /** What the record currently carries. */
  tags: TagAssignment[]
  categoriesLoading?: boolean
  /**
   * Offers a field for free tags — tags on no axis, typed in. Omit it where free
   * tags are turned off: the record's free tags are then left alone, as a tag on
   * an axis not passed is.
   */
  freeTags?: FreeTagsField
  /**
   * Applies the change. Returns true to close, false to keep the form open —
   * the caller owns how tags are saved and how a failure is reported, because
   * that differs per area.
   */
  onSave: (changes: TagChanges) => Promise<boolean>
  onFormComplete: () => void
  onFormCancel: () => void
  title?: string
  /**
   * Permission the modal gates on, checked by useModalForm. Omitted where tagging
   * is granted by membership rather than a claim, and the caller opens the form
   * only for someone the record's capability flag allows.
   */
  permission?: string
}

const toTagIds = (value: string | string[] | undefined): string[] => {
  if (Array.isArray(value)) return value
  return value ? [value] : []
}

const tagIdsByCategory = (tags: TagAssignment[]) => {
  const grouped = new Map<string, string[]>()
  for (const tag of tags) {
    if (!tag.categoryId) continue
    grouped.set(tag.categoryId, [
      ...(grouped.get(tag.categoryId) ?? []),
      tag.tagId,
    ])
  }
  return grouped
}

const freeTagIds = (tags: TagAssignment[]) =>
  tags.filter((tag) => !tag.categoryId).map((tag) => tag.tagId)

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
 * Free tag options: those suggested plus those the record carries, which a
 * suggestion list may not hold, so their chips still read as names.
 */
const freeOptionsFor = (field: FreeTagsField, tags: TagAssignment[]) => {
  const byId = new Map(field.options.map((tag) => [tag.id, tag.name]))
  for (const tag of tags) {
    if (!tag.categoryId && !byId.has(tag.tagId))
      byId.set(tag.tagId, tag.tagName)
  }
  return [...byId]
    .map(([value, label]) => ({ value, label }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))
}

/**
 * Splits the free tag field's value into existing tags and new names. A typed
 * name that matches a known free tag, ignoring case, is that tag: the field
 * cannot tell a new entry from a reselection, and the server would apply the
 * existing tag for it anyway.
 */
const splitFreeValues = (
  values: string[],
  options: { value: string; label: string }[],
) => {
  const ids = new Set(options.map((option) => option.value))
  const byName = new Map(
    options.map((option) => [option.label.toLowerCase(), option.value]),
  )

  const tagIds = new Set<string>()
  const names = new Map<string, string>()
  for (const raw of values) {
    if (ids.has(raw)) {
      tagIds.add(raw)
      continue
    }
    const name = normalizeName(raw)
    if (!name) continue
    const known = byName.get(name.toLowerCase())
    if (known) tagIds.add(known)
    else if (!names.has(name.toLowerCase())) names.set(name.toLowerCase(), name)
  }

  return { tagIds: [...tagIds], names: [...names.values()] }
}

/**
 * Applies a record's tags across every offered axis at once, and its free tags
 * when the area allows them.
 *
 * Holds no knowledge of what is being tagged: categories, current tags and the
 * save all arrive as props, so any area with tags can use it.
 *
 * A category the caller does not pass is left alone — its tags never enter the
 * diff. That is what keeps a tag on a deactivated axis from being stripped by a
 * save that never offered it. Free tags follow the same rule.
 */
const ManageTagsForm = ({
  categories,
  tags,
  categoriesLoading = false,
  freeTags,
  onSave,
  onFormComplete,
  onFormCancel,
  title = 'Manage Tags',
  permission,
}: ManageTagsFormProps) => {
  const freeOptions = freeTags ? freeOptionsFor(freeTags, tags) : []

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

        const addedNames: string[] = []
        if (freeTags) {
          const before = freeTagIds(tags)
          const { tagIds, names } = splitFreeValues(
            toTagIds(values[FREE_TAGS_FIELD]),
            freeOptions,
          )
          // Refused here, before any change is sent: the server would refuse the
          // name only after the other changes had already applied.
          if (names.some((name) => name.length > MAX_TAG_NAME_LENGTH)) {
            form.setFields([
              { name: FREE_TAGS_FIELD, errors: [TAG_NAME_TOO_LONG] },
            ])
            return false
          }
          added.push(...tagIds.filter((id) => !before.includes(id)))
          removed.push(...before.filter((id) => !tagIds.includes(id)))
          addedNames.push(...names)
        }

        return await onSave({ added, removed, addedNames })
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the tags. Please try again.',
      permission,
    })

  const hasFreeTags = !!freeTags
  useEffect(() => {
    const current = tagIdsByCategory(tags)
    const values: ManageTagsFormValues = {}
    for (const category of categories) {
      const tagIds = current.get(category.id) ?? []
      values[category.id] = category.allowsMany ? tagIds : tagIds[0]
    }
    if (hasFreeTags) values[FREE_TAGS_FIELD] = freeTagIds(tags)
    form.setFieldsValue(values)
  }, [tags, categories, hasFreeTags, form])

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
      <Spin spinning={categoriesLoading || !!freeTags?.loading}>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="manage-tags-form"
        >
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
                showSearch={{ optionFilterProp: 'label' }}
              />
            </Form.Item>
          ))}
          {freeTags && (
            <Form.Item
              name={FREE_TAGS_FIELD}
              label="Free tags"
              extra="Pick an existing tag or type a new one and press Enter."
              rules={[
                {
                  validator: async (_, value: string[] | undefined) => {
                    const known = new Set(freeOptions.map((o) => o.value))
                    const tooLong = (value ?? []).some(
                      (v) =>
                        !known.has(v) &&
                        normalizeName(v).length > MAX_TAG_NAME_LENGTH,
                    )
                    if (tooLong) throw new Error(TAG_NAME_TOO_LONG)
                  },
                },
              ]}
            >
              <Select
                mode="tags"
                allowClear
                options={freeOptions}
                placeholder="Add tags"
                showSearch={{ optionFilterProp: 'label' }}
                tokenSeparators={[',']}
                maxTagTextLength={64}
              />
            </Form.Item>
          )}
        </Form>
      </Spin>
    </Modal>
  )
}

export default ManageTagsForm
