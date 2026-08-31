'use client'

import { ManageTagsForm, TagChanges } from '@/src/components/common/tags'
import { useMessage } from '@/src/components/contexts/messaging'
import { ProductDto } from '@/src/services/wayd-api'
import { useGetProductTagCategoriesQuery } from '@/src/store/features/product-management/product-tag-categories-api'
import {
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
 * This adapter owns the two things that are Product Management's rather than
 * tagging's: where the axes come from, and how a change is saved.
 */
const ManageProductTagsForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ManageProductTagsFormProps) => {
  const messageApi = useMessage()

  const [tagProduct] = useTagProductMutation()
  const [untagProduct] = useUntagProductMutation()

  // Active axes only. A tag on a deactivated axis is therefore never offered and
  // never diffed, so it stays on the product — deactivating retires an axis from
  // new use rather than stripping what already carries it.
  const { data: categories, isLoading } = useGetProductTagCategoriesQuery(true)

  const onSave = async ({ added, removed }: TagChanges) => {
    // Each change is its own request, so a failure part-way through leaves the
    // earlier ones applied. Counting them is what lets the failure path say so
    // instead of implying nothing happened.
    let applied = 0
    const total = removed.length + added.length

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
      categoriesLoading={isLoading}
      tags={product.tags ?? []}
      onSave={onSave}
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
      permission="Permissions.Products.Update"
    />
  )
}

export default ManageProductTagsForm
