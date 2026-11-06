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
import { TagCategoryTagsList, useTagCategoryActions } from '../_components'
import TagCategoryDetailsLoading from './loading'
import { TagCategoryFacts } from './_components'

enum TagCategorySections {
  Tags = 'tags',
}

const TagCategoryDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const messageApi = useMessage()
  const router = useRouter()

  // The list is the only read of a category — there is no per-record endpoint —
  // so every mutation's invalidation of it refreshes this page too. The same
  // arguments as the list page, so both share one cache entry.
  const {
    data: categories,
    isLoading,
    error,
    refetch,
  } = useGetIntakeRequestTagCategoriesQuery({ includeInactive: true })

  const category = categories?.find((a) => a.id === id)

  const { hasPermissionClaim } = useAuth()
  // A platform-defined category is read-only whatever the viewer holds: the domain
  // refuses to add, change or remove its tags.
  const canManageTags =
    hasPermissionClaim('Permissions.IntakeRequests.Administer') &&
    category?.isSystem === false

  useDocumentTitle(
    category ? `${category.name} - Tag Category` : 'Tag Category Details',
  )

  const { getActionItems, dialogs } = useTagCategoryActions({
    onChanged: refetch,
    onDeleted: () => router.push('/settings/demand-management/request-tags'),
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while loading the tag category',
      )
      console.error(error)
    }
  }, [error, messageApi])

  if (isLoading) {
    return <TagCategoryDetailsLoading />
  }

  if (!category) {
    return notFound()
  }

  const actionItems = getActionItems(category)

  // One section, so `RecordLayout` renders no rail — the tags are the whole of
  // the record's content, and the rest of it is the facts.
  const sections: RecordSection[] = [
    {
      id: TagCategorySections.Tags,
      label: 'Tags',
      count: category.tags?.length,
    },
  ]

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={TagCategorySections.Tags}
        record={{
          name: category.name,
          parent: {
            label: 'Request Tags',
            href: '/settings/demand-management/request-tags',
          },
          subtitle: 'Tag Category',
          actions:
            actionItems.length > 0 ? (
              <PageActions actionItems={actionItems} />
            ) : undefined,
        }}
        facts={<TagCategoryFacts category={category} />}
      >
        {() => (
          <TagCategoryTagsList
            category={category}
            canManageTags={canManageTags}
            loadData={refetch}
          />
        )}
      </RecordLayout>

      {dialogs}
    </>
  )
}

const TagCategoryDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    TagCategoryDetailsPage,
    'Permission',
    'Permissions.IntakeRequests.Administer',
  ),
  'demand-management',
)

export default TagCategoryDetailsPageWithAuthorization
