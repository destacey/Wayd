import { BackgroundJobTypeDto } from '@/src/services/wayd-api'
import { toSchedulableOptions } from './create-recurring-job-form'

const jobType = (
  id: number,
  name: string,
  isSchedulable: boolean,
): BackgroundJobTypeDto =>
  ({
    id,
    name,
    description: '',
    order: id,
    groupName: 'Test Jobs',
    isSchedulable,
  }) as BackgroundJobTypeDto

describe('create-recurring-job-form helpers', () => {
  describe('toSchedulableOptions', () => {
    it('should omit job types that cannot be scheduled', () => {
      const jobTypes = [
        jobType(0, 'People Full Sync', true),
        jobType(1003, 'Iterations Sync', false),
      ]

      const options = toSchedulableOptions(jobTypes)

      expect(options).toEqual([{ value: 0, label: 'People Full Sync' }])
    })

    it('should offer every schedulable job type', () => {
      const jobTypes = [
        jobType(0, 'People Full Sync', true),
        jobType(1, 'Work Full Sync', true),
        jobType(2000, 'Portfolio Rank Rebalance', true),
      ]

      const options = toSchedulableOptions(jobTypes)

      expect(options).toHaveLength(3)
      expect(options.map((o) => o.value)).toEqual([0, 1, 2000])
    })

    it('should return no options when nothing is schedulable', () => {
      // Offering a non-schedulable type produced a 500 from the recurring
      // endpoint, which is the bug this filter exists to prevent.
      const jobTypes = [
        jobType(1001, 'Strategic Themes Sync', false),
        jobType(1002, 'Projects Sync', false),
      ]

      expect(toSchedulableOptions(jobTypes)).toEqual([])
    })

    it('should map each option to the id and display name', () => {
      const options = toSchedulableOptions([jobType(3, 'People Diff Sync', true)])

      expect(options[0]).toEqual({ value: 3, label: 'People Diff Sync' })
    })
  })
})
