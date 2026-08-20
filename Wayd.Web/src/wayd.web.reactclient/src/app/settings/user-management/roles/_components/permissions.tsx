import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import useTheme from '@/src/components/contexts/theme'
import { useGetPermissionsQuery } from '@/src/store/features/user-management/permissions-api'
import { useUpdatePermissionsMutation } from '@/src/store/features/user-management/roles-api'
import {
  Button,
  Collapse,
  Divider,
  Flex,
  Input,
  Space,
  Spin,
  Switch,
  Tag,
  Typography,
} from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { useEffect, useState } from 'react'
import { RoleDto } from '@/src/services/wayd-api'

const { Title } = Typography

interface PermissionsProps {
  role: RoleDto
  permissions: string[]
  isSystemRole: boolean
  onDirtyChange?: (isDirty: boolean) => void
}

interface PermissionItem {
  name: string
  description: string
}

interface PermissionGroup {
  name: string
  permissions: PermissionItem[]
}

interface PermissionCategory {
  name: string
  groups: PermissionGroup[]
}

const Permissions = (props: PermissionsProps) => {
  const sourcePermissions = props.permissions ?? []
  const [permissions, setPermissions] = useState<string[]>(sourcePermissions)
  const [searchText, setSearchText] = useState('')
  const [expandedKeys, setExpandedKeys] = useState<string[]>([])
  const [isEditMode, setIsEditMode] = useState(false)
  const [isSaving, setIsSaving] = useState(false)

  const { onDirtyChange } = props

  const { hasPermissionClaim } = useAuth()
  const messageApi = useMessage()
  const theme = useTheme()

  const canUpdate =
    hasPermissionClaim('Permissions.Roles.Update') && !props.isSystemRole

  const { data: permissionsData, isLoading } = useGetPermissionsQuery()

  const [updatePermissions] = useUpdatePermissionsMutation()

  const isDirty = (() => {
    if (!isEditMode) return false
    if (permissions.length !== sourcePermissions.length) return true
    const sorted = [...permissions].sort()
    const sortedProps = [...sourcePermissions].sort()
    return sorted.some((p, i) => p !== sortedProps[i])
  })()

  useEffect(() => {
    onDirtyChange?.(isDirty)
  }, [isDirty, onDirtyChange])

  useEffect(() => {
    if (!isDirty) return

    const message =
      'You have unsaved permission changes. Are you sure you want to leave?'

    // Browser navigation (refresh, close tab, external URL)
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = message
    }

    // Client-side navigation (capture phase to intercept before Next.js)
    const handleClick = (e: MouseEvent) => {
      const anchor = (e.target as HTMLElement).closest('a')
      if (!anchor) return
      const href = anchor.getAttribute('href')
      if (!href || href === '#') return
      if (
        anchor.target === '_blank' ||
        anchor.hasAttribute('download') ||
        anchor.href === window.location.href
      )
        return
      if (!window.confirm(message)) {
        e.preventDefault()
        e.stopPropagation()
      }
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    document.addEventListener('click', handleClick, true)
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload)
      document.removeEventListener('click', handleClick, true)
    }
  }, [isDirty])

  const categories = (() => {
    if (!permissionsData) return []
    const categoryMap = new Map<string, Map<string, PermissionItem[]>>()

    permissionsData.forEach((permission) => {
      const categoryName = permission.category || 'Other'
      if (!categoryMap.has(categoryName)) {
        categoryMap.set(categoryName, new Map())
      }
      const groupMap = categoryMap.get(categoryName)!
      if (!groupMap.has(permission.resource)) {
        groupMap.set(permission.resource, [])
      }
      groupMap.get(permission.resource)!.push({
        name: permission.name,
        description: permission.description,
      })
    })

    const result: PermissionCategory[] = []
    categoryMap.forEach((groupMap, categoryName) => {
      const groups: PermissionGroup[] = []
      groupMap.forEach((perms, resourceName) => {
        groups.push({ name: resourceName, permissions: perms })
      })
      groups.sort((a, b) => a.name.localeCompare(b.name))
      result.push({ name: categoryName, groups })
    })

    return result.sort((a, b) => a.name.localeCompare(b.name))
  })()

  const allGroups = categories.flatMap((c) => c.groups)

  const groupTotalCountMap = (() => {
    const map = new Map<string, number>()
    allGroups.forEach((group) => {
      map.set(group.name, group.permissions.length)
    })
    return map
  })()

  const filteredCategories = (() => {
    if (!searchText) return categories
    const lower = searchText.toLowerCase()
    return categories
      .map((category) => {
        const categoryMatch = category.name.toLowerCase().includes(lower)
        if (categoryMatch) return category
        const filteredGroups = category.groups
          .map((group) => {
            const groupMatch = group.name.toLowerCase().includes(lower)
            if (groupMatch) return group
            const filteredPerms = group.permissions.filter(
              (p) =>
                p.name.toLowerCase().includes(lower) ||
                p.description.toLowerCase().includes(lower),
            )
            if (filteredPerms.length === 0) return null
            return { ...group, permissions: filteredPerms }
          })
          .filter(Boolean) as PermissionGroup[]
        if (filteredGroups.length === 0) return null
        return { ...category, groups: filteredGroups }
      })
      .filter(Boolean) as PermissionCategory[]
  })()

  const allFilteredGroups = filteredCategories.flatMap((c) => c.groups)

  const effectivePermissionsSet = new Set(isEditMode ? permissions : sourcePermissions)

  const displayCategories = (() => {
    if (isEditMode) return filteredCategories
    return filteredCategories
      .map((category) => {
        const groups = category.groups
          .map((group) => {
            const permissionsInRole = group.permissions.filter((p) =>
              effectivePermissionsSet.has(p.name),
            )
            if (permissionsInRole.length === 0) return null
            return { ...group, permissions: permissionsInRole }
          })
          .filter(Boolean) as PermissionGroup[]
        if (groups.length === 0) return null
        return { ...category, groups }
      })
      .filter(Boolean) as PermissionCategory[]
  })()

  const allDisplayGroups = displayCategories.flatMap((c) => c.groups)

  const hasPermission = (permission: string) => effectivePermissionsSet.has(permission)

  const handlePermissionChange = (item: PermissionItem) => {
    if (!hasPermission(item.name)) {
      setPermissions([...permissions, item.name])
    } else {
      setPermissions(permissions.filter((p) => p !== item.name))
    }
  }

  const handleToggleGroup = (group: PermissionGroup, checked: boolean) => {
    const permSet = new Set(permissions)
    group.permissions.forEach((p) => {
      if (checked) {
        permSet.add(p.name)
      } else {
        permSet.delete(p.name)
      }
    })
    setPermissions(Array.from(permSet))
  }

  const handleSelectAll = (select: boolean) => {
    if (select) {
      const allNames = allGroups.flatMap((g) =>
        g.permissions.map((p) => p.name),
      )
      setPermissions([...new Set([...permissions, ...allNames])])
    } else {
      const allNames = new Set(
        allGroups.flatMap((g) => g.permissions.map((p) => p.name)),
      )
      setPermissions(permissions.filter((p) => !allNames.has(p)))
    }
  }

  const handleExpandAll = () => {
    setExpandedKeys(allDisplayGroups.map((g) => g.name))
  }

  const handleCollapseAll = () => {
    setExpandedKeys([])
  }

  const isAllExpanded =
    allDisplayGroups.length > 0 &&
    expandedKeys.length === allDisplayGroups.length

  const getGroupSelectedCount = (group: PermissionGroup) =>
    group.permissions.filter((p) => hasPermission(p.name)).length

  const handleSave = async () => {
    setIsSaving(true)
    try {
      await updatePermissions({
        roleId: props.role.id,
        permissions: permissions,
      })

      messageApi.success('Permissions saved successfully')
      setIsEditMode(false)
      onDirtyChange?.(false)
    } catch (error) {
      messageApi.error('Failed to save permissions')
    } finally {
      setIsSaving(false)
    }
  }

  const handleCancelEdit = () => {
    setIsEditMode(false)
    setPermissions(sourcePermissions)
    onDirtyChange?.(false)
  }

  if (isLoading) return <Spin size="small" />

  const buildCollapseItems = (groups: PermissionGroup[]) =>
    groups.map((group) => {
      const selectedCount = getGroupSelectedCount(group)
      const totalCount =
        groupTotalCountMap.get(group.name) ?? group.permissions.length
      const allSelected = selectedCount === totalCount

      return {
        key: group.name,
        label: <span style={{ fontWeight: 600 }}>{group.name}</span>,
        extra: (
          <Flex align="center" gap={8} onClick={(e) => e.stopPropagation()}>
            <Tag
              color={
                selectedCount === totalCount
                  ? 'success'
                  : selectedCount > 0
                    ? 'warning'
                    : undefined
              }
            >
              {selectedCount} of {totalCount}
            </Tag>
            {isEditMode && canUpdate ? (
              <>
                <span
                  style={{
                    fontSize: 13,
                    color: theme.token.colorTextSecondary,
                  }}
                >
                  Toggle All
                </span>
                <Switch
                  size="small"
                  checked={allSelected}
                  onChange={(checked) => handleToggleGroup(group, checked)}
                />
              </>
            ) : null}
          </Flex>
        ),
        children: (
          <div>
            {group.permissions.map((permission, idx) => (
              <div key={permission.name}>
                {idx > 0 && <Divider style={{ margin: '8px 0' }} />}
                <Flex
                  justify="space-between"
                  align="center"
                  style={{ padding: '4px 0' }}
                >
                  <div>
                    <div style={{ fontWeight: 600 }}>
                      {permission.description}
                    </div>
                    <div
                      style={{
                        fontSize: 13,
                        color: theme.token.colorTextSecondary,
                      }}
                    >
                      {permission.name}
                    </div>
                  </div>
                  {isEditMode && canUpdate ? (
                    <Switch
                      checked={hasPermission(permission.name)}
                      onChange={() => handlePermissionChange(permission)}
                    />
                  ) : null}
                </Flex>
              </div>
            ))}
          </div>
        ),
      }
    })

  return (
    <div>
      <Flex
        align="center"
        justify="space-between"
        style={{ marginBottom: 16, gap: 12, flexWrap: 'wrap' }}
      >
        <Input
          placeholder="Search permissions..."
          prefix={<SearchOutlined />}
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          allowClear
          style={{ maxWidth: 300, flex: '1 1 220px' }}
        />
        <Space>
          {isEditMode && canUpdate ? (
            <>
              <a onClick={() => handleSelectAll(true)}>Select All</a>
              <a onClick={() => handleSelectAll(false)}>Unselect All</a>
              <a onClick={isAllExpanded ? handleCollapseAll : handleExpandAll}>
                {isAllExpanded ? 'Collapse All' : 'Expand All'}
              </a>
              <Button onClick={handleCancelEdit}>Cancel</Button>
              <Button
                type="primary"
                loading={isSaving}
                onClick={handleSave}
                disabled={!isDirty}
              >
                Save Permissions
              </Button>
            </>
          ) : (
            <>
              <a onClick={isAllExpanded ? handleCollapseAll : handleExpandAll}>
                {isAllExpanded ? 'Collapse All' : 'Expand All'}
              </a>
              {canUpdate && (
                <Button
                  type="default"
                  onClick={() => {
                    setPermissions(sourcePermissions)
                    setIsEditMode(true)
                  }}
                >
                  Manage Permissions
                </Button>
              )}
            </>
          )}
        </Space>
      </Flex>

      {displayCategories.map((category) => (
        <div key={category.name} style={{ marginBottom: 16 }}>
          <Title level={5} style={{ marginBottom: 8 }}>
            {category.name}
          </Title>
          <Collapse
            activeKey={expandedKeys}
            onChange={(keys) => setExpandedKeys(keys as string[])}
            items={buildCollapseItems(category.groups)}
          />
        </div>
      ))}
    </div>
  )
}

export default Permissions
