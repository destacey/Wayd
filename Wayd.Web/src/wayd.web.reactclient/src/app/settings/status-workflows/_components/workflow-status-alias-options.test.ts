import { WorkflowAliasDto, WorkflowStatusDto } from '@/src/services/wayd-api'
import { buildAliasOptions } from './workflow-status-alias-options'

const ALIASES: WorkflowAliasDto[] = [
  { value: 1, name: 'Ready', isRequired: true },
  { value: 2, name: 'Released', isRequired: true },
  { value: 3, name: 'Withdrawn', isRequired: false },
]

const status = (id: string, alias: number): WorkflowStatusDto => ({
  id,
  name: `Status ${id}`,
  category: { id: 2, name: 'Active' },
  alias,
  order: 1,
})

describe('buildAliasOptions', () => {
  it('always offers None first', () => {
    // Arrange / Act
    const options = buildAliasOptions(ALIASES, [])

    // Assert
    expect(options[0]).toEqual({ value: 0, label: 'None' })
  })

  it('drops an alias another status already holds', () => {
    // Arrange — only one status may carry a given meaning, so offering a taken
    // one would build a workflow the server refuses.
    const statuses = [status('a', 2)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses)

    // Assert
    expect(options.map((o) => o.value)).toEqual([0, 1, 3])
  })

  it('keeps the alias of the status being edited', () => {
    // Arrange
    const statuses = [status('a', 2), status('b', 1)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, 'a')

    // Assert — 'a' keeps Released; 'b' still blocks Ready
    expect(options.map((o) => o.value)).toEqual([0, 2, 3])
  })

  it('marks required meanings so the publish rule is visible while choosing', () => {
    // Arrange / Act
    const options = buildAliasOptions(ALIASES, [])

    // Assert
    expect(options.map((o) => o.label)).toEqual([
      'None',
      'Ready (required)',
      'Released (required)',
      'Withdrawn',
    ])
  })

  it('tolerates a workflow with no statuses or an owner type with no aliases', () => {
    // Arrange / Act / Assert
    expect(buildAliasOptions(undefined, undefined)).toEqual([
      { value: 0, label: 'None' },
    ])
  })
})
