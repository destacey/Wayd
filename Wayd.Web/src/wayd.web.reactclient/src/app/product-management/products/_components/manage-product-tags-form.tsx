'use client'

import { ManageTagsForm, TagChanges } from '@/src/components/common/tags'
import { useMessage } from '@/src/components/contexts/messaging'
import { ProductDto } from '@/src/services/wayd-api'
import { useGetProductTagSettingsQuery } from '@/src/store/features/admin/system-settings-api'
import { useGetProductTagCategoriesQuery } from '@/src/store/features/product-management/product-tag-categories-api'
import { useSearchProductTagsQuery } from '@/src/store/features/product-management/product-tags-api'
import {
  useTagProductByNameMutation,
  useTagProductMutation,
  useUntagProductMutation,
} from '@/src/store/features/product-management/products-api'
import { isApiError, type ApiError } from '@/src/utils'

export interface ManageProductTagsFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Applies a product's tags, over the shared tag manager.
 *
 * This adapter owns the things that are Product Management's rather than
 * tagging's: where the axes and free tags come from, whether free tags are
 * offered at all, and how a change is saved.
 */
const ManageProductTagsForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ManageProductTagsFormProps) => {
  const messageApi = useMessage()

  const [tagProduct] = useTagProductMutation()
  const [tagProductByName] = useTagProductByNameMutation()
  const [untagProduct] = useUntagProductMutation()

  // Active axes only. A tag on a deactivated axis is therefore never offered and
  // never diffed, so it stays on the product — deactivating retires an axis from
  // new use rather than stripping what already carries it.
  const { data: categories, isLoading } = useGetProductTagCategoriesQuery(true)

  // With free tags off the field is not offered, so the product's existing free
  // tags are left alone rather than stripped by the next save.
  const { data: tagSettings, isLoading: settingsLoading } =
    useGetProductTagSettingsQuery()
  const allowsFreeTags = tagSettings?.allowsFreeTags ?? false
  const { data: freeTags, isLoading: freeTagsLoading } =
    useSearchProductTagsQuery({ isFree: true }, { skip: !allowsFreeTags })

  const onSave = async ({ added, removed, addedNames }: TagChanges) => {
    // Each change is its own request, so a failure part-way through leaves the
    // earlier ones applied. Counting them is what lets the failure path say so
    // instead of implying nothing happened.
    let applied = 0
    const total = removed.length + added.length + addedNames.length

    try {
      // Removals first: on a single-value axis, tagging before untagging would
      // have the domain replace the tag and the untag would then strip it.
      for (const tagId of removed) {
        const response = await untagProduct({ id: product.id, tagId })
        if (response.error) throw response.error
        applied++
      }
      for (const tagId of added) {
        const response = await tagProduct({ id: product.id, tagId })
        if (response.error) throw response.error
        applied++
      }
      // Names last: one may resolve to a tag on an axis, which should replace
      // rather than be replaced by an id applied above.
      for (const name of addedNames) {
        const response = await tagProductByName({ id: product.id, name })
        if (response.error) throw response.error
        applied++
      }

      messageApi.success('Product tags updated successfully.')
      return true
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      const detail =
        apiError.detail ??
        'An error occurred while updating the product tags. Please try again.'

      if (applied > 0) {
        messageApi.error(
          `${detail} ${applied} of ${total} changes were saved — reopen Manage Tags to see the current tags.`,
        )

        // Closing is deliberate. The form's baseline is the product prop, which
        // is now behind the server by exactly the changes that did apply, so
        // leaving it open invites a second save that re-adds what was just
        // removed. onFormComplete refetches, so reopening shows the truth.
        onFormComplete()
        return false
      }

      messageApi.error(detail)
      return false
    }
  }

  return (
    <ManageTagsForm
      categories={categories ?? []}
      categoriesLoading={isLoading || settingsLoading}
      tags={product.tags ?? []}
      freeTags={
        allowsFreeTags
          ? {
              options: (freeTags ?? []).map((tag) => ({
                id: tag.id,
                name: tag.name,
                description: tag.description,
                isActive: tag.isActive,
              })),
              loading: freeTagsLoading,
            }
          : undefined
      }
      onSave={onSave}
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
      permission="Permissions.Products.Update"
    />
  )
}

export default ManageProductTagsForm
