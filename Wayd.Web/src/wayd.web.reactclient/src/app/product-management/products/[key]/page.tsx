'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout } from '@/src/components/common/record'
import type { RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetProductQuery,
  useGetProductsQuery,
} from '@/src/store/features/product-management/products-api'
import { Button, MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { use, useState } from 'react'
// Imported directly rather than through the barrel: the barrel also pulls in the
// create form, and with it the markdown editor's ESM-only dependencies, which this
// page never renders.
import ChangeProductStatusForm from '../_components/change-product-status-form'
import CreateProductForm from '../_components/create-product-form'
import DeleteProductForm from '../_components/delete-product-form'
import EditProductForm from '../_components/edit-product-form'
import ManageProductTagsForm from '../_components/manage-product-tags-form'
import ReparentProductForm from '../_components/reparent-product-form'
import RetypeProductForm from '../_components/retype-product-form'
import ProductsGrid from '../_components/products-grid'
import ProductFacts from './_components/product-facts'
import ProductOverview from './_components/product-overview'
import ProductDetailsLoading from './loading'

enum ProductSections {
  Overview = 'overview',
  // "products", not "components": a child is a Product like any other, and its
  // type decides what kind — an Application can sit under an Application. Naming
  // the section after one type would mislabel the rest.
  Products = 'products',
}

const ProductDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)

  const [isEditOpen, setIsEditOpen] = useState<boolean>(false)
  const [isDeleteOpen, setIsDeleteOpen] = useState<boolean>(false)
  const [isCreateChildOpen, setIsCreateChildOpen] = useState<boolean>(false)
  const [isChangeStatusOpen, setIsChangeStatusOpen] = useState<boolean>(false)
  const [isRetypeOpen, setIsRetypeOpen] = useState<boolean>(false)
  const [isReparentOpen, setIsReparentOpen] = useState<boolean>(false)
  const [isManageTagsOpen, setIsManageTagsOpen] = useState<boolean>(false)
  const router = useRouter()

  // The active section lives in the URL (?section=), owned by RecordLayout. Read
  // here because sectionActions renders for whichever section is open, so an
  // unconditional action would appear on every one of them.
  const searchParams = useSearchParams()
  const activeSection = searchParams.get('section') ?? ProductSections.Overview

  const { hasPermissionClaim } = useAuth()
  const canUpdateProduct = hasPermissionClaim('Permissions.Products.Update')
  const canDeleteProduct = hasPermissionClaim('Permissions.Products.Delete')
  const canCreateProduct = hasPermissionClaim('Permissions.Products.Create')

  const {
    data: product,
    error,
    isLoading,
    refetch,
  } = useGetProductQuery(key)

  useDocumentTitle(product ? `${product.name} - Product` : 'Product')

  // The children of this node, for the Components section. Filtered server-side
  // so the page does not pull the whole catalogue to find them.
  const { data: components, isLoading: componentsLoading } =
    useGetProductsQuery(
      { parentId: product?.id },
      { skip: !product?.id },
    )

  if ((error as { status?: number })?.status === 404) {
    notFound()
  }

  if (isLoading || !product) {
    return <ProductDetailsLoading />
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    if (canUpdateProduct) {
      items.push({ key: 'edit', label: 'Edit', onClick: () => setIsEditOpen(true) })
      // Each is its own action rather than a field on Edit: every one carries a
      // rule the API enforces, and a blanket save would hide which one refused.
      items.push({
        key: 'change-status',
        label: 'Change Status',
        onClick: () => setIsChangeStatusOpen(true),
      })
      items.push({
        key: 'retype',
        label: 'Change Type',
        onClick: () => setIsRetypeOpen(true),
      })
      items.push({
        key: 'reparent',
        label: 'Move',
        onClick: () => setIsReparentOpen(true),
      })
      items.push({
        key: 'manage-tags',
        label: 'Manage Tags',
        onClick: () => setIsManageTagsOpen(true),
      })
    }

    if (canDeleteProduct) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setIsDeleteOpen(true),
      })
    }

    return items
  })()

  const sections: RecordSection[] = [
    { id: ProductSections.Overview, label: 'Overview' },
    {
      id: ProductSections.Products,
      label: 'Products',
      // The count belongs on the tab: whether a product has parts is the first
      // thing a reader wants, and opening an empty section to find out is worse.
      count: components?.length || undefined,
    },
  ]

  const renderSection = (section: string) => {
    if (section === ProductSections.Products) {
      return (
        <ProductsGrid
          products={components ?? []}
          isLoading={componentsLoading}
          refetch={refetch}
          // Flat: every row here already shares this product as its parent, so
          // the tree would add a level that says nothing.
          asTree={false}
          persistStateKey="product-management-product-children"
        />
      )
    }

    return (
      <ProductOverview
        product={product}
        childProducts={components ?? []}
        childProductsLoading={componentsLoading}
        onNavigateToSection={(sectionId) =>
          router.replace(`?section=${sectionId}`, { scroll: false })
        }
        productsSectionId={ProductSections.Products}
      />
    )
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ProductSections.Overview}
        record={{
          name: product.name,
          subtitle: 'Product Details',
          // Outermost first: the list always, then the parent when there is one,
          // so a nested product shows where it sits without losing the way back.
          parent: [
            { label: 'Products', href: '/product-management/products' },
            ...(product.parent
              ? [
                  {
                    label: product.parent.name,
                    href: `/product-management/products/${product.parent.key}`,
                  },
                ]
              : []),
          ],
          recordKey: String(product.key),
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        sectionActions={
          canCreateProduct && activeSection === ProductSections.Products ? (
            <Button onClick={() => setIsCreateChildOpen(true)}>
              Add Product
            </Button>
          ) : undefined
        }
        facts={<ProductFacts product={product} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {isEditOpen && (
        <EditProductForm
          product={product}
          onFormComplete={() => {
            setIsEditOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsEditOpen(false)}
        />
      )}

      {isCreateChildOpen && (
        <CreateProductForm
          // Preselected, so adding a part from within a product does not ask the
          // reader to find the product they are already looking at.
          defaultParentId={product.id}
          onFormComplete={() => {
            setIsCreateChildOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsCreateChildOpen(false)}
        />
      )}

      {isChangeStatusOpen && (
        <ChangeProductStatusForm
          product={product}
          onFormComplete={() => {
            setIsChangeStatusOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsChangeStatusOpen(false)}
        />
      )}

      {isRetypeOpen && (
        <RetypeProductForm
          product={product}
          onFormComplete={() => {
            setIsRetypeOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsRetypeOpen(false)}
        />
      )}

      {isReparentOpen && (
        <ReparentProductForm
          product={product}
          onFormComplete={() => {
            setIsReparentOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsReparentOpen(false)}
        />
      )}

      {isManageTagsOpen && (
        <ManageProductTagsForm
          product={product}
          onFormComplete={() => {
            setIsManageTagsOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsManageTagsOpen(false)}
        />
      )}

      {isDeleteOpen && (
        <DeleteProductForm
          product={product}
          onFormComplete={() => {
            setIsDeleteOpen(false)
            // The record no longer exists, so returning to the list is the only valid destination.
            router.push('/product-management/products')
          }}
          onFormCancel={() => setIsDeleteOpen(false)}
        />
      )}
    </>
  )
}

const ProductDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(ProductDetailsPage, 'Permission', 'Permissions.Products.View'),
  'product-management',
)

export default ProductDetailsPageWithAuthorization
