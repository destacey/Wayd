'use client'

import { ManageTagsForm, TagChanges } from '@/src/components/common/tags'
import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import {
  useGetIntakeRequestTagCategoriesQuery,
  useTagIntakeRequestMutation,
  useUntagIntakeRequestMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { isApiError, type ApiError } from '@/src/utils'

export interface ManageIntakeRequestTagsFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Applies a request's tags over the shared tag manager. Opened only when the request's `canChangeTags`
 * says so: tagging is granted by being an agent on the request's desk, not by a claim.
 */
const ManageIntakeRequestTagsForm = ({
  request,
  onFormComplete,
  onFormCancel,
}: ManageIntakeRequestTagsFormProps) => {
  const messageApi = useMessage()
  const { summary } = request

  const [tagRequest] = useTagIntakeRequestMutation()
  const [untagRequest] = useUntagIntakeRequestMutation()

  // The axes the request's desk offers, so its agents choose only from what applies to them.
  const { data: offered, isLoading: offeredLoading } =
    useGetIntakeRequestTagCategoriesQuery({ deskId: summary.desk.id })
  // Every axis, for one the request already carries a tag on but its desk does not offer — a request moved
  // here keeps it — so that tag can still be removed. Not offered for new tags beyond what it carries.
  const { data: all, isLoading: allLoading } =
    useGetIntakeRequestTagCategoriesQuery({ includeInactive: true })
  const isLoading = offeredLoading || allLoading
  const offeredIds = new Set((offered ?? []).map((c) => c.id))
  const carriedOnly = (all ?? [])
    .filter(
      (c) =>
        !offeredIds.has(c.id) &&
        summary.tags.some((t) => t.categoryId === c.id),
    )
    .map((c) => ({
      ...c,
      tags: c.tags.filter((t) => summary.tags.some((st) => st.id === t.id)),
    }))
  const categories = [...(offered ?? []), ...carriedOnly]

  const onSave = async ({ added, removed }: TagChanges) => {
    // Each change is its own request, so a failure part-way leaves the earlier ones applied.
    let applied = 0
    const total = removed.length + added.length

    try {
      // Removals first: on a single-value axis, tagging first would replace the tag and the untag would
      // then strip the new one.
      for (const tagId of removed) {
        const response = await untagRequest({ requestId: summary.id, tagId })
        if (response.error) throw response.error
        applied++
      }
      for (const tagId of added) {
        const response = await tagRequest({ requestId: summary.id, tagId })
        if (response.error) throw response.error
        applied++
      }

      messageApi.success('Request tags updated.')
      return true
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      const detail =
        apiError.detail ??
        'An error occurred while updating the request tags. Please try again.'

      if (applied > 0) {
        messageApi.error(
          `${detail} ${applied} of ${total} changes were saved — reopen Manage Tags to see the current tags.`,
        )
        // The form's baseline is now behind the server, so a second save from it would undo what applied.
        onFormComplete()
        return false
      }

      messageApi.error(detail)
      return false
    }
  }

  return (
    <ManageTagsForm
      categories={categories.map((c) => ({
        id: c.id,
        name: c.name,
        description: c.description,
        allowsMany: c.allowsMany,
        tags: c.tags,
      }))}
      categoriesLoading={isLoading}
      tags={summary.tags.map((t) => ({
        tagId: t.id,
        tagName: t.name,
        categoryId: t.categoryId,
        categoryName: t.categoryName,
      }))}
      onSave={onSave}
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    />
  )
}

export default ManageIntakeRequestTagsForm
