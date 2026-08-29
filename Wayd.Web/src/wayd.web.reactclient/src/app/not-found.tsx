'use client'

import { Result } from 'antd'

// This is the global 404 Not Found page for handling missing resources. It should not be
// used by components routing 404s, which are handled by Next.js automatically. Components
// should use the `notFound` function from 'next/navigation' to trigger a 404.
export default function NotFound() {
  return (
    <div className="page-gutters">
      <Result status="404" title="404" subTitle="Resource not found"></Result>
    </div>
  )
}
