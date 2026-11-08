'use client'

import MyTeamSprints from '@/src/components/common/planning/my-team-sprints'
import { Skeleton } from 'antd'
import { FC, useState } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'

type SprintsState = 'loading' | 'none' | 'some' | 'error'

/**
 * The active sprints of the caller's teams, one section each in a single
 * card. `MyTeamSprints` draws nothing until it knows, and nothing when there
 * are none, so the skeleton, the empty state and the error are drawn here
 * from what it reports. It stays mounted throughout, since it is what finds out.
 */
const MyTeamSprintsWidget: FC<HomeWidgetProps> = ({ title }) => {
  const [state, setState] = useState<SprintsState>('loading')

  return (
    <WidgetCard title={title}>
      {state === 'loading' ? (
        <Skeleton active title={false} paragraph={{ rows: 3 }} />
      ) : null}
      {state === 'none' ? (
        <WidgetEmpty message="None of your teams has an active sprint. A team's sprint appears here while it runs." />
      ) : null}
      {state === 'error' ? (
        <WidgetError message="Could not load your teams." />
      ) : null}
      <MyTeamSprints
        onHasSprintsChange={(hasSprints) =>
          setState(hasSprints ? 'some' : 'none')
        }
        onLoadError={() => setState('error')}
      />
    </WidgetCard>
  )
}

export default MyTeamSprintsWidget
