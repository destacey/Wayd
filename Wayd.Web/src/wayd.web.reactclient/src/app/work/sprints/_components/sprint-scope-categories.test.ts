import {
  SprintScopeEntry,
  SprintScopeItemDto,
  SprintScopeOutcome,
} from '@/src/services/wayd-api'
import {
  SprintScopeCategory,
  isInSprintScopeCategory,
} from './sprint-scope-categories'

const item = (
  entry: SprintScopeEntry,
  outcome: SprintScopeOutcome,
): SprintScopeItemDto => ({ entry, outcome }) as unknown as SprintScopeItemDto

describe('isInSprintScopeCategory', () => {
  it('counts an item completed as Removed as completed', () => {
    const removed = item(SprintScopeEntry.Committed, SprintScopeOutcome.Removed)

    expect(
      isInSprintScopeCategory(removed, SprintScopeCategory.Completed),
    ).toBe(true)
  })

  it('puts an item under both its entry and its outcome', () => {
    const addedThenCarried = item(
      SprintScopeEntry.Added,
      SprintScopeOutcome.CarriedOver,
    )

    expect(
      isInSprintScopeCategory(addedThenCarried, SprintScopeCategory.Added),
    ).toBe(true)
    expect(
      isInSprintScopeCategory(
        addedThenCarried,
        SprintScopeCategory.CarriedOver,
      ),
    ).toBe(true)
    expect(
      isInSprintScopeCategory(addedThenCarried, SprintScopeCategory.Committed),
    ).toBe(false)
  })

  it('shows every item under All', () => {
    const descoped = item(
      SprintScopeEntry.Committed,
      SprintScopeOutcome.Descoped,
    )

    expect(isInSprintScopeCategory(descoped, SprintScopeCategory.All)).toBe(
      true,
    )
  })
})
