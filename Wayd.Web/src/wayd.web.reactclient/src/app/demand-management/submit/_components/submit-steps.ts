import type { NamePath } from 'antd/es/form/interface'

/** The steps, and the fields each must have right before the requester moves on. */
export const STEPS: { title: string; fields: NamePath[] }[] = [
  {
    title: 'The ask',
    fields: ['requesterId', 'title', 'description', 'channelId'],
  },
  {
    title: 'Why and when',
    fields: [
      'expectedBenefits',
      ['needBy', 'date'],
      ['needBy', 'basis'],
      ['needBy', 'reference'],
      ['needBy', 'committedById'],
      ['needBy', 'committedOn'],
    ],
  },
  { title: 'Check and submit', fields: [] },
]

const sameName = (a: NamePath, b: NamePath) =>
  JSON.stringify([a].flat()) === JSON.stringify([b].flat())

/**
 * The first step holding any of the fields, so an error on a step the requester has left takes them back
 * to it rather than failing out of sight. Undefined when none of them belongs to a step.
 */
export const stepWithError = (names: NamePath[]): number | undefined => {
  const index = STEPS.findIndex((s) =>
    s.fields.some((f) => names.some((n) => sameName(f, n))),
  )
  return index < 0 ? undefined : index
}
