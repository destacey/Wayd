import { EmployeeNavigationDto } from '@/src/services/wayd-api'

/** Someone a message can mention: who the composer offers after an @. */
export interface MentionCandidate {
  id: string
  name: string
}

/**
 * A mention as a message stores it, `@[Name](employee:{id})`. The server reads
 * only the id; the name is what the author saw, and readers are shown the
 * person's current name instead.
 */
const storedMention =
  /@\[([^\]\r\n]{1,200})\]\(employee:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)/g

const escapeRegExp = (text: string) =>
  text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

const escapeLinkText = (text: string) => text.replace(/[[\]\\]/g, '\\$&')

/**
 * A message's markdown ready to render: each mention becomes a link to the
 * person, under their current name. A mention the server dropped, or of
 * someone it no longer names, reads as plain text, so nothing links to a person
 * the reader was not shown.
 */
export const toDisplayMarkdown = (
  body: string,
  mentions: EmployeeNavigationDto[],
): string => {
  const byId = new Map(mentions.map((m) => [m.id.toLowerCase(), m]))
  return body.replace(storedMention, (_, name: string, id: string) => {
    const person = byId.get(id.toLowerCase())
    return person
      ? `[@${escapeLinkText(person.name)}](/organizations/employees/${person.key})`
      : `@${name}`
  })
}

/**
 * A stored message as the composer edits it: each mention as `@Name`, with the
 * people it names, so saving the edit writes them back as mentions.
 */
export const toComposerText = (
  body: string,
  mentions: EmployeeNavigationDto[],
): { text: string; mentioned: MentionCandidate[] } => {
  const byId = new Map(mentions.map((m) => [m.id.toLowerCase(), m]))
  const mentioned = new Map<string, MentionCandidate>()
  const text = body.replace(storedMention, (_, name: string, id: string) => {
    const shown = byId.get(id.toLowerCase())?.name ?? name
    mentioned.set(id.toLowerCase(), { id, name: shown })
    return `@${shown}`
  })
  return { text, mentioned: [...mentioned.values()] }
}

/**
 * What the composer wrote, with each `@Name` of someone picked from its list
 * written as a stored mention. Longer names are matched first, so "@Ann Lee"
 * is never taken for "@Ann". A name typed but never picked stays plain text.
 */
export const fromComposerText = (
  text: string,
  mentioned: MentionCandidate[],
): string => {
  const byName = new Map<string, MentionCandidate>()
  for (const person of mentioned) byName.set(person.name, person)
  const names = [...byName.keys()].sort((a, b) => b.length - a.length)
  if (names.length === 0) return text

  const pattern = new RegExp(
    `@(${names.map(escapeRegExp).join('|')})(?![\\w])`,
    'g',
  )
  return text.replace(pattern, (_, name: string) => {
    const person = byName.get(name)!
    return `@[${person.name}](employee:${person.id})`
  })
}
