'use client'

import { ComponentType, FC } from 'react'
import NotAuthorized from '../common/not-authorized'
import useAuth from '../contexts/auth'

/**
 * Props for the withAuthorization higher-order component.
 * @param claimType - The type of claim to check. Default is "Permission".
 * @param claimValue - The value of the claim to check.
 * @param notAuthorizedBehavior - Component will not be rendered if set to true. Default is to render "Not Authorized"
 */
export interface WithAuthorizationProps {
  claimType?: string | never // The type of claim to check. Default is "Permission".
  claimValue?: string | never // The value of the claim to check.
  notAuthorizedBehavior?: 'NotAuthorized' | 'DoNotRender' // Component will not be rendered if set to true. Default is to render "Not Authorized"
}

/**
 * Wrapper for pages that require authorization.
 * Will redirect to the not authorized page if the user does not have the listed claim.
 * @param WrappedComponent - The component to wrap.
 * @param defaultClaimType - If claimType is not provided to the wrapped component, this will be used. Default is "Permission".
 * @param defaultClaimValue - If claimValue is not provided to the wrapped component, this will be used.
 */
const withAuthorization = <P extends object>(
  WrappedComponent: ComponentType<P>,
  defaultClaimType?: string,
  defaultClaimValue?: string,
): FC<P & WithAuthorizationProps> => {
  const wrappedComponentName =
    WrappedComponent.displayName || WrappedComponent.name || 'Component'

  const WithAuthorization: ComponentType<P & WithAuthorizationProps> = ({
    claimType,
    claimValue,
    notAuthorizedBehavior,
    ...props
  }) => {
    const { hasClaim } = useAuth()

    return hasClaim(
      claimType ?? defaultClaimType ?? 'Permission',
      (claimValue ?? defaultClaimValue ?? ''),
    ) ? (
      <WrappedComponent {...(props as P)} />
    ) : notAuthorizedBehavior === 'DoNotRender' ? (
      <></>
    ) : (
      <NotAuthorized />
    )
  }

  WithAuthorization.displayName = `withAuthorization(${wrappedComponentName})`

  return WithAuthorization
}

export default withAuthorization
