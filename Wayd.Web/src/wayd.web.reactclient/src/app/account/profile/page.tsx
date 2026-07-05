'use client'

import React, { useEffect, useState } from 'react'
import { Button, Card } from 'antd'
import { usePathname } from 'next/navigation'
import PageTitle from '../../../components/common/page-title'
import ProfileForm from './profile-form'
import ChangePasswordForm from './change-password-form'
import ClaimsGrid from './claims-grid'
import PersonalAccessTokens from './personal-access-tokens'
import Preferences from './preferences'
import useAuth from '../../../components/contexts/auth'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { useGetProfileQuery } from '@/src/store/features/user-management/profile-api'
import { useMessage } from '@/src/components/contexts/messaging'
enum AccountTabs {
  Profile = 'profile',
  Preferences = 'preferences',
  Claims = 'claims',
  PersonalAccessTokens = 'personalAccessTokens',
}

const tabs = [
  { key: AccountTabs.Profile, tab: 'Profile' },
  { key: AccountTabs.Preferences, tab: 'Preferences' },
  { key: AccountTabs.PersonalAccessTokens, tab: 'PATs' },
  { key: AccountTabs.Claims, tab: 'Claims' },
]

const AccountProfilePage = () => {
  useDocumentTitle('Account Profile')
  const [activeTab, setActiveTab] = useState(AccountTabs.Profile)
  const [openChangePasswordForm, setOpenChangePasswordForm] = useState(false)

  const messageApi = useMessage()
  const { user, isLoading, authMethod } = useAuth()

  const dispatch = useAppDispatch()
  const pathname = usePathname()

  const {
    data: profileData,
    isLoading: profileLoading,
    error: profileError,
  } = useGetProfileQuery(undefined, { skip: !user })

  const isLocalUser = authMethod === 'local'

  const renderTabContent = () => {
    switch (activeTab) {
      case AccountTabs.Profile:
        return React.createElement(ProfileForm, profileData)
      case AccountTabs.Preferences:
        return <Preferences />
      case AccountTabs.PersonalAccessTokens:
        return <PersonalAccessTokens />
      case AccountTabs.Claims:
        return <ClaimsGrid />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as AccountTabs)
  }

  useEffect(() => {
    if (!isLoading && user) {
      dispatch(setBreadcrumbTitle({ title: user.name, pathname }))
    }
  }, [user, isLoading, pathname, dispatch])

  useEffect(() => {
    if (profileError) {
      console.error(profileError)
      messageApi.error('Failed to load user profile.')
    }
  }, [profileError, messageApi])

  const actions = isLocalUser ? (
    <Button onClick={() => setOpenChangePasswordForm(true)}>
      Change Password
    </Button>
  ) : undefined

  return (
    <>
      <PageTitle title="Account" subtitle="Manage your account" actions={actions} />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>
      {openChangePasswordForm && (
        <ChangePasswordForm
          onFormComplete={() => setOpenChangePasswordForm(false)}
          onFormCancel={() => setOpenChangePasswordForm(false)}
        />
      )}
    </>
  )
}

export default AccountProfilePage
