/**
 * Turns a record's name into the kebab-cased stem of a downloaded file.
 *
 * Everything outside a-z0-9 collapses to a single dash, so a name carrying spaces, an ampersand or
 * punctuation ("Trust & Safety Reporting") cannot produce a file name the operating system rejects or
 * a shell reads as two arguments.
 */
export const toFileName = (text: string) =>
  text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
