import { PropsWithChildren } from 'react'
import LegacyPageChrome from '../_components/legacy-page-chrome'

/**
 * Pages that have not yet adopted `RecordLayout`.
 *
 * Supplies the page padding and breadcrumb row that used to live on
 * `.app-main-content`. They moved here because a page cannot un-pad its
 * parent — so the decision belongs to the route group rather than to the app
 * shell, which stays unaware of which pages have migrated.
 *
 * Route groups do not affect URLs: `(legacy)/ppm/projects` still serves
 * `/ppm/projects`. Migrating a page means moving its folder to `(records)/`.
 * When this group is empty, delete it along with `LegacyPageChrome`.
 */
const LegacyLayout = ({ children }: PropsWithChildren) => (
  <LegacyPageChrome>{children}</LegacyPageChrome>
)

export default LegacyLayout
