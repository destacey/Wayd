'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateProductForm, ProductsGrid } from './_components'

const ProductsPage: FC = () => {
  useDocumentTitle('Products')
  const [openCreateProductForm, setOpenCreateProductForm] =
    useState<boolean>(false)
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
        persistStateKey="product-management-products"
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
