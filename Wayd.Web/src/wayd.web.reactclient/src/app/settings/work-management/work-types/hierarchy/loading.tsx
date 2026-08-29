'use client'

import { RecordLoading } from '@/src/components/common/record'

// Matches the page's `RecordShell` frame rather than a padded list page —
// loading.tsx renders instead of the page, so a mismatched frame makes the
// content jump when the record arrives.
export default function WorkTypeHierarchyLoading() {
  return <RecordLoading title="Work Type Hierarchy" />
}
