import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { Steps } from 'antd'
import dayjs from 'dayjs'

export interface WorkItemStepsProps {
  workItem: WorkItemDetailsDto
}

const WorkItemSteps = ({ workItem }: WorkItemStepsProps) => {
  if (!workItem) return null

  let currentStep: number = 0
  switch (workItem.statusCategory.name) {
    case 'Proposed':
      currentStep = 0
      break
    case 'Active':
      currentStep = 1
      break
    case 'Done':
      currentStep = 2
      break
    case 'Removed':
      currentStep = 3
      break
    default:
      return 0
  }

  const stepItems = [
    {
      title: 'Proposed',
      content: `Created on ${dayjs(workItem.created).format('MMM D, YYYY @ h:mm A')}`,
    },
    {
      title: 'Active',
      content:
        currentStep === 0
          ? 'Work has not started'
          : workItem.activatedTimestamp
            ? `Work started on ${dayjs(workItem.activatedTimestamp).format('MMM D, YYYY @ h:mm A')}`
            : 'Work was not started',
    },
    {
      title: currentStep < 3 ? 'Done' : 'Removed',
      content:
        currentStep < 2
          ? 'Work has not completed'
          : currentStep === 2
            ? `Work completed on ${dayjs(workItem.doneTimestamp).format('MMM D, YYYY @ h:mm A')}`
            : 'Work removed',
    },
  ]

  return (
    <Steps type="dot" current={currentStep} items={stepItems} size="small" />
  )
}

export default WorkItemSteps
