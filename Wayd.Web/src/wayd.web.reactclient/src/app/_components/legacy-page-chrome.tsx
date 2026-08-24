'use client'

import { PropsWithChildren } from 'react'
import AppBreadcrumb from './app-breadcrumb'

/**
 * Page padding and the breadcrumb row, for pages that have not adopted
 * `RecordLayout`.
 *
 * These used to live on `.app-main-content` in the app shell, which meant a
 * page could not opt out of them — a child cannot un-pad its parent, and
 * negative margins only appear to work until something clips. Moving them into
 * a wrapper lets each route decide, without the shell knowing which pages have
 * migrated.
 *
 * Applied via `app/(legacy)/layout.tsx`. When every page renders its own
 * chrome, this component and the route group can both be deleted.
 */
const LegacyPageChrome = ({ children }: PropsWithChildren) => (
  <div className="legacy-page-chrome">
    <AppBreadcrumb />
    {children}
  </div>
)

export default LegacyPageChrome
