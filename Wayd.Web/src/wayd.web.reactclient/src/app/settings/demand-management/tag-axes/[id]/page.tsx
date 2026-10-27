'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { useGetIntakeRequestTagCategoriesQuery } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect } from 'react'
import { TagAxisTagsList, useTagAxisActions } from '../_components'
import TagAxisDetailsLoading from './loading'
import { TagAxisFacts } from './_components'

enum TagAxisSections {
  Tags = 'tags',
}

const TagAxisDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const messageApi = useMessage()
  const router = useRouter()

  // The list is the only read of an axis — there is no per-record endpoint —
  // so every mutation's invalidation of it refreshes this page too. The same
  // arguments as the list page, so both share one cache entry.
  const {
    data: axes,
    isLoading,
    error,
    refetch,
  } = useGetIntakeRequestTagCategoriesQuery({ includeInactive: true })

  const axis = axes?.find((a) => a.id === id)

  const { hasPermissionClaim } = useAuth()
  // A platform-defined axis is read-only whatever the viewer holds: the domain
  // refuses to add, change or remove its tags.
  const canManageTags =
    hasPermissionClaim('Permissions.IntakeRequests.Administer') &&
    axis?.isSystem === false

  useDocumentTitle(axis ? `${axis.name} - Tag Axis` : 'Tag Axis Details')

  const { getActionItems, dialogs } = useTagAxisActions({
    onChanged: refetch,
    onDeleted: () => router.push('/settings/demand-management/tag-axes'),
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while loading the tag axis',
      )
      console.error(error)
    }
  }, [error, messageApi])

  if (isLoading) {
    return <TagAxisDetailsLoading />
  }

  if (!axis) {
    return notFound()
  }

  const actionItems = getActionItems(axis)

  // One section, so `RecordLayout` renders no rail — the tags are the whole of
  // the record's content, and the rest of it is the facts.
  const sections: RecordSection[] = [
    { id: TagAxisSections.Tags, label: 'Tags', count: axis.tags?.length },
  ]

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={TagAxisSections.Tags}
        record={{
          name: axis.name,
          parent: {
            label: 'Tag Axes',
            href: '/settings/demand-management/tag-axes',
          },
          subtitle: 'Tag Axis',
          actions:
            actionItems.length > 0 ? (
              <PageActions actionItems={actionItems} />
            ) : undefined,
        }}
        facts={<TagAxisFacts axis={axis} />}
      >
        {() => (
          <TagAxisTagsList
            axis={axis}
            canManageTags={canManageTags}
            loadData={refetch}
          />
        )}
      </RecordLayout>

      {dialogs}
    </>
  )
}

const TagAxisDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    TagAxisDetailsPage,
    'Permission',
    'Permissions.IntakeRequests.Administer',
  ),
  'demand-management',
)

export default TagAxisDetailsPageWithAuthorization
