import React from 'react'
import { render } from '@testing-library/react'
import '@testing-library/jest-dom'

import { AuthContext, AuthContextType } from '../contexts/auth'
import withAuthorization, { WithAuthorizationProps } from './with-authorization'

describe('withAuthorization', () => {
  const MockComponent = () => <div>Authorized</div>

  const mockHasClaim = jest.fn()

  jest.mock('../contexts/auth', () => ({
    __esModule: true,
    default: () => ({
      hasClaim: mockHasClaim,
      hasPermissionClaim: mockHasClaim,
    }),
  }))

  beforeEach(() => {
    jest.clearAllMocks()
  })

  const mockProps: WithAuthorizationProps = {
    claimValue: 'testClaim',
  }

  const renderComponent = (component: any) => {
    const authContext: AuthContextType = {
      user: null,
      isLoading: false,
      acquireToken: () => Promise.resolve('token'),
      refreshUser: () => Promise.resolve(),
      login: () => Promise.resolve(),
      logout: () => Promise.resolve(),
      hasClaim: mockHasClaim,
      hasPermissionClaim: mockHasClaim,
      authMethod: null,
      loginProvider: null,
      mustChangePassword: false,
      localLogin: () => Promise.resolve(),
    }

    return render(
      <AuthContext.Provider value={authContext}>
        {component}
      </AuthContext.Provider>,
    )
  }

  it('renders the wrapped component if the user has the required claim', () => {
    mockHasClaim.mockReturnValue(true)

    const WrappedComponent = withAuthorization(MockComponent)
    const { getByText } = renderComponent(<WrappedComponent {...mockProps} />)

    expect(getByText('Authorized')).toBeInTheDocument()
    expect(mockHasClaim).toHaveBeenCalledWith(
      'Permission',
      mockProps.claimValue,
    )
  })

  it('renders the NotAuthorized component if the user does not have the required claim', () => {
    mockHasClaim.mockReturnValue(false)

    const WrappedComponent = withAuthorization(MockComponent)
    const { getByText } = renderComponent(<WrappedComponent {...mockProps} />)

    expect(getByText('403')).toBeInTheDocument()
    expect(
      getByText('Sorry, you are not authorized to access this page.'),
    ).toBeInTheDocument()
    expect(mockHasClaim).toHaveBeenCalledWith(
      'Permission',
      mockProps.claimValue,
    )
  })

  it('does not render a component if the user does not have the required claim', () => {
    mockHasClaim.mockReturnValue(false)

    const WrappedComponent = withAuthorization(MockComponent)
    const { queryByText } = renderComponent(
      <WrappedComponent
        claimType="TestType"
        claimValue="TestValue"
        notAuthorizedBehavior="DoNotRender"
      />,
    )

    expect(queryByText('Authorized', { exact: false })).toBeNull()
    expect(mockHasClaim).toHaveBeenCalledWith('TestType', 'TestValue')
  })

  it('does not render a component if the user does not have the required default claim', () => {
    mockHasClaim.mockReturnValue(false)

    const WrappedComponent = withAuthorization(
      MockComponent,
      'TestType',
      'TestValue',
    )
    const { queryByText } = renderComponent(
      <WrappedComponent notAuthorizedBehavior="DoNotRender" />,
    )

    expect(queryByText('Authorized', { exact: false })).toBeNull()
    expect(mockHasClaim).toHaveBeenCalledWith('TestType', 'TestValue')
  })
})
