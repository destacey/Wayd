import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'

/**
 * What an action or its dialog needs to know about a category.
 *
 * Narrower than the details DTO on purpose: the list rows carry all of it, so
 * the row `⋯` and the panel `⋯` are built by the same call rather than one of
 * them needing a fetched record first.
 *
 * Its own module so the dialogs and the actions hook can both name it without
 * importing each other.
 */
export type ExpenditureCategoryActionTarget = Pick<
  ExpenditureCategoryDetailsDto,
  'id' | 'name' | 'state'
>
