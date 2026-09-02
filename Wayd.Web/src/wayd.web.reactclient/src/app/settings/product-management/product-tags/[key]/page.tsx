'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { useGetProductTagCategoriesQuery } from '@/src/store/features/product-management/product-tag-categories-api'
import { isApiError } from '@/src/utils'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useMemo } from 'react'
import {
  ProductTagsList,
  useProductTagCategoryActions,
} from '../_components'
import ProductTagCategoryDetailsLoading from './loading'
import { ProductTagCategoryFacts } from './_components'

enum ProductTagCategorySections {
  Tags = 'tags',
}

const ProductTagCategoryDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)

  const messageApi = useMessage()
  const router = useRouter()

  // The catalog is the only read of a category — there is no per-record
  // endpoint — so the record comes out of the list the settings page already
  // holds. That also means every mutation's invalidation of the list refreshes
  // this page for free.
  const {
    data: categories,
    isLoading,
    error,
    refetch,
  } = useGetProductTagCategoriesQuery(undefined)

  const category = useMemo(
    () => categories?.find((c) => String(c.key) === String(key)),
    [categories, key],
  )

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.ProductTagCategories.Update')

  // A platform-seeded axis is read-only whatever the viewer holds: the domain
  // refuses to add, rename or retire its tags, so offering the actions would
  // only produce a failure.
  const canManageTags = canUpdate && category?.isSystem === false

  useDocumentTitle(
    category ? `${category.name} - Tag Category` : 'Tag Category Details',
  )

  const { getActionItems, dialogs } = useProductTagCategoryActions({
    onChanged: refetch,
    onDeleted: () => router.push('/settings/product-management/product-tags'),
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the tag category',
      )
      console.error(error)
    }
  }, [error, messageApi])

  if (isLoading) {
    return <ProductTagCategoryDetailsLoading />
  }

  if (!category) {
    return notFound()
  }

  const actionItems = getActionItems(category)

  // One section, so `RecordLayout` renders no rail — the tags are the whole of
  // the record's content, and the rest of it is the facts.
  const sections: RecordSection[] = [
    {
      id: ProductTagCategorySections.Tags,
      label: 'Tags',
      count: category.tags?.length,
    },
  ]

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ProductTagCategorySections.Tags}
        record={{
          name: category.name,
          recordKey: String(category.key),
          parent: {
            label: 'Product Tags',
            href: '/settings/product-management/product-tags',
          },
          subtitle: 'Tag Category',
          actions:
            actionItems.length > 0 ? (
              <PageActions actionItems={actionItems} />
            ) : undefined,
        }}
        facts={<ProductTagCategoryFacts category={category} />}
      >
        {() => (
          <ProductTagsList
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

const ProductTagCategoryDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    ProductTagCategoryDetailsPage,
    'Permission',
    'Permissions.ProductTagCategories.View',
  ),
  'product-management',
)

export default ProductTagCategoryDetailsPageWithAuthorization
