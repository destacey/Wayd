import { PropsWithChildren } from 'react'

/**
 * Pages that render their own chrome via `RecordLayout`.
 *
 * Deliberately adds nothing: no padding, no breadcrumb. `RecordLayout` runs
 * edge-to-edge so the section rail sits flush against the app sider and the
 * identity bar spans the full width — which only works if nothing above it
 * has already added padding.
 *
 * The identity bar's `parent` link replaces the breadcrumb: on a record page
 * the crumb only ever read "Area / List / Details", which the identity bar
 * already says, in less vertical space.
 *
 * This layout exists to be a no-op. When every page has adopted, the padding
 * can move onto `.app-main-content` for good and both groups can go away.
 */
const RecordsLayout = ({ children }: PropsWithChildren) => <>{children}</>

export default RecordsLayout
