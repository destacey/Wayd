import { ProductTagCategoryDto } from '@/src/services/wayd-api'

/**
 * What an action or its dialog needs to know about a tag axis.
 *
 * Narrower than the DTO on purpose: the list rows carry all of it, so the row
 * `⋯` and the record page's `⋯` are built by the same call rather than one of
 * them needing a fetched record first.
 *
 * `isSystem` is in it because it decides which actions exist at all — a
 * platform-seeded axis can be deactivated but never edited or deleted.
 */
export type ProductTagCategoryActionTarget = Pick<
  ProductTagCategoryDto,
  'id' | 'key' | 'name' | 'description' | 'order' | 'isActive' | 'isSystem'
>
