'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { ClusterOutlined, MenuOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { FC, useEffect, useState } from 'react'
import { CreateProductForm, ProductsGrid } from './_components'

type ProductsView = 'Tree' | 'List'

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: 'Tree',
    icon: <ClusterOutlined alt="Tree view" title="Tree view" />,
  },
  {
    value: 'List',
    icon: <MenuOutlined alt="List view" title="List view" />,
  },
]

const ProductsPage: FC = () => {
  useDocumentTitle('Products')
  const [openCreateProductForm, setOpenCreateProductForm] =
    useState<boolean>(false)
  // Tree by default: products are a hierarchy, and a flat list hides what a component is part of.
  const [currentView, setCurrentView] = useState<ProductsView>('Tree')
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateProduct = hasPermissionClaim('Permissions.Products.Create')

  const {
    data: productData,
    isLoading,
    error,
    refetch,
  } = useGetProductsQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load products.')
    }
  }, [error, messageApi])

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={(value) => setCurrentView(value as ProductsView)}
    />
  )

  const actions = !canCreateProduct ? null : (
    <Button onClick={() => setOpenCreateProductForm(true)}>
      Create Product
    </Button>
  )

  const onCreateProductFormClosed = (wasCreated: boolean) => {
    setOpenCreateProductForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Products" actions={actions} />
      <ProductsGrid
        products={productData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        viewSelector={viewSelector}
        asTree={currentView === 'Tree'}
        // Separate keys per view: the two show different columns, so one layout cannot serve both.
        persistStateKey={
          currentView === 'Tree'
            ? 'product-management-products-tree'
            : 'product-management-products-list'
        }
      />
      {openCreateProductForm && (
        <CreateProductForm
          onFormComplete={() => onCreateProductFormClosed(true)}
          onFormCancel={() => onCreateProductFormClosed(false)}
        />
      )}
    </div>
  )
}

const ProductsPageWithAuthorization = requireFeatureFlag(
  authorizePage(ProductsPage, 'Permission', 'Permissions.Products.View'),
  'product-management',
)

export default ProductsPageWithAuthorization
