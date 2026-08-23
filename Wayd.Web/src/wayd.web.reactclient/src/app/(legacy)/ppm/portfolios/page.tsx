'use client'

import { PageTitle } from '@/src/components/common'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetPortfoliosQuery } from '@/src/store/features/ppm/portfolios-api'
import { AppstoreOutlined, MenuOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { FC, useEffect, useState } from 'react'
import {
  CreatePortfolioForm,
  PortfoliosCardGrid,
  PortfoliosGrid,
} from './_components'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'

enum Views {
  Cards,
  List,
}

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: Views.Cards,
    icon: <AppstoreOutlined alt="Cards" title="Cards" />,
  },
  {
    value: Views.List,
    icon: <MenuOutlined alt="List" title="List" />,
  },
]

const PortfoliosPage: FC = () => {
  useDocumentTitle('Portfolios')
  const [currentView, setCurrentView] = useState<string | number>(Views.Cards)
  const [openCreatePortfolioForm, setOpenCreatePortfolioForm] =
    useState<boolean>(false)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreatePortfolio = hasPermissionClaim(
    'Permissions.ProjectPortfolios.Create',
  )
  const showActions = canCreatePortfolio

  const {
    data: portfolioData,
    isLoading,
    error,
    refetch,
  } = useGetPortfoliosQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load portfolios.')
    }
  }, [error, messageApi])

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  const actions = !showActions ? null : (
    <>
      {canCreatePortfolio && (
        <Button onClick={() => setOpenCreatePortfolioForm(true)}>
          Create Portfolio
        </Button>
      )}
    </>
  )

  const onCreatePortfolioFormClosed = (wasCreated: boolean) => {
    setOpenCreatePortfolioForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Portfolios" actions={actions} />
      {currentView === Views.Cards ? (
        <PortfoliosCardGrid
          portfolios={portfolioData ?? []}
          viewSelector={viewSelector}
          isLoading={isLoading}
        />
      ) : (
        <PortfoliosGrid
          portfolios={portfolioData ?? []}
          viewSelector={viewSelector}
          isLoading={isLoading}
          refetch={refetch}
          persistStateKey="ppm-portfolios"
        />
      )}
      {openCreatePortfolioForm && (
        <CreatePortfolioForm
          onFormComplete={() => onCreatePortfolioFormClosed(true)}
          onFormCancel={() => onCreatePortfolioFormClosed(false)}
        />
      )}
    </>
  )
}

const PortfoliosPageWithAuthorization = authorizePage(
  PortfoliosPage,
  'Permission',
  'Permissions.ProjectPortfolios.View',
)

export default PortfoliosPageWithAuthorization
