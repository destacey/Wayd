import { Breadcrumb, Typography } from 'antd'
import type {
  BreadcrumbItemType,
  BreadcrumbSeparatorType,
  ItemType,
} from 'antd/es/breadcrumb/Breadcrumb'
import Link from 'next/link'

const { Text } = Typography

export interface BasicBreadcrumbProps {
  items: ItemType[]
}

const BasicBreadcrumb = ({ items }: BasicBreadcrumbProps) => {
  if (!items || items.length === 0) {
    return null
  }

  const itemRender = (
    route: Partial<BreadcrumbItemType & BreadcrumbSeparatorType>,
    params: any,
    routes: Partial<BreadcrumbItemType & BreadcrumbSeparatorType>[],
    paths: string[],
  ) => {
    const last = routes.indexOf(route) === routes.length - 1
    return last || !route.href ? (
      <Text>{route.title}</Text>
    ) : (
      <Link href={route.href}>{route.title}</Link>
    )
  }

  return (
    <Breadcrumb
      separator=">"
      style={{ marginBottom: '16px' }}
      itemRender={itemRender}
      items={items}
    />
  )
}

export default BasicBreadcrumb
