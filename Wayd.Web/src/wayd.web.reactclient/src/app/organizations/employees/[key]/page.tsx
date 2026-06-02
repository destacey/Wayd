'use client'

import PageTitle from '@/src/components/common/page-title'
import { use, useEffect, useState } from 'react'
import EmployeeDetails from './employee-details'
import { Card, MenuProps, Spin } from 'antd'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname, useRouter } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { InactiveTag, PageActions } from '@/src/components/common'
import { useGetEmployeeQuery } from '@/src/store/features/organizations/employee-api'
import EmployeeDetailsLoading from './loading'
import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import DeleteEmployeeForm from '../_components/delete-employee-form'
import EmployeeTeamsGrid from './_components/employee-teams-grid'
import { CloseOutlined } from '@ant-design/icons'
import dynamic from 'next/dynamic'

const EmployeeCycleTimeReport = dynamic(
  () =>
    import('@/src/components/common/work/cycle-time-report').then((mod) => ({
      default: mod.EmployeeCycleTimeReport,
    })),
  { ssr: false, loading: () => <Spin /> },
)

enum EmployeeTabs {
  Details = 'details',
  Teams = 'teams',
  CycleTimeReport = 'cycle-time-report',
}

const tabs = [
  {
    key: EmployeeTabs.Details,
    tab: 'Details',
  },
  {
    key: EmployeeTabs.Teams,
    tab: 'Teams',
  },
]

const EmployeeDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const employeeKey = Number(key)

  const [activeTab, setActiveTab] = useState(EmployeeTabs.Details)
  const [openDeleteEmployeeForm, setOpenDeleteEmployeeForm] =
    useState<boolean>(false)
  const [dynamicTabs, setDynamicTabs] = useState<
    Array<{ key: string; tab: string; closable: boolean }>
  >([])

  const messageApi = useMessage()
  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canDeleteEmployee = hasPermissionClaim('Permissions.Employees.Delete')

  const {
    data: employeeData,
    isLoading,
    error,
  } = useGetEmployeeQuery(employeeKey)

  useDocumentTitle(
    employeeData?.displayName
      ? `${employeeData.displayName} - Employee Details`
      : 'Employee Details',
  )

  const renderTabContent = () => {
    switch (activeTab) {
      case EmployeeTabs.Details:
        return <EmployeeDetails employee={employeeData!} />
      case EmployeeTabs.Teams:
        return <EmployeeTeamsGrid employeeId={employeeData!.id} />
      case EmployeeTabs.CycleTimeReport:
        return <EmployeeCycleTimeReport employeeId={employeeData!.id} />
      default:
        return null
    }
  }

  const openCycleTimeReport = () => {
    const cycleTimeTabExists = dynamicTabs.some(
      (tab) => tab.key === EmployeeTabs.CycleTimeReport,
    )

    if (!cycleTimeTabExists) {
      setDynamicTabs((prevTabs) => [
        ...prevTabs,
        {
          key: EmployeeTabs.CycleTimeReport,
          tab: 'Cycle Time Report',
          closable: true,
        },
      ])
    }

    setActiveTab(EmployeeTabs.CycleTimeReport)
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as EmployeeTabs)
  }

  const closeTab = (tabKey: string, e: React.MouseEvent) => {
    e.stopPropagation()
    setDynamicTabs((prevTabs) => prevTabs.filter((tab) => tab.key !== tabKey))

    if (activeTab === tabKey) {
      setActiveTab(EmployeeTabs.Details)
    }
  }

  useEffect(() => {
    dispatch(setBreadcrumbTitle({ title: 'Details', pathname }))
  }, [dispatch, pathname])

  useEffect(() => {
    if (error) {
      messageApi.error('Failed to load employee details.')
    }
  }, [error, messageApi])

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []
    if (canDeleteEmployee) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setOpenDeleteEmployeeForm(true),
      })
    }

    if (items.length > 0) {
      items.push({ type: 'divider', key: 'divider-reports' })
    }

    items.push({
      type: 'group',
      label: 'Reports',
      children: [
        {
          key: 'cycle-time-report',
          label: 'Cycle Time Report',
          onClick: openCycleTimeReport,
        },
      ],
    })

    return items
  })()

  const allTabs = (() => {
    const closableTabs = dynamicTabs.map((tab) => ({
      key: tab.key,
      tab: (
        <span>
          {tab.tab}
          <CloseOutlined
            style={{ marginLeft: 8 }}
            onClick={(e) => closeTab(tab.key, e)}
          />
        </span>
      ),
    }))

    return [...tabs, ...closableTabs]
  })()

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteEmployeeForm(false)
    if (wasDeleted) {
      router.push('/organizations/employees/')
    }
  }

  if (isLoading) {
    return <EmployeeDetailsLoading />
  }

  if (!employeeData) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title={`${employeeData?.key} - ${employeeData?.displayName}`}
        subtitle="Employee Details"
        tags={<InactiveTag isActive={employeeData?.isActive} />}
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Card
        style={{ width: '100%' }}
        tabList={allTabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>

      {/* Delete Employee Form */}
      {openDeleteEmployeeForm && (
        <DeleteEmployeeForm
          employeeKey={employeeData.key}
          onFormComplete={() => {
            onDeleteFormClosed(true)
          }}
          onFormCancel={() => {
            onDeleteFormClosed(false)
          }}
        />
      )}
    </>
  )
}

const EmployeeDetailsPageWithAuthorization = authorizePage(
  EmployeeDetailsPage,
  'Permission',
  'Permissions.Employees.View',
)

export default EmployeeDetailsPageWithAuthorization
