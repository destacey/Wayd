import { defaultSchema } from 'rehype-sanitize'

/**
 * Sanitize schema for user-supplied markdown. The default schema drops `<iframe>`, `<form>`,
 * `<base>`, `<meta>`, `<style>` and event-handler/style attributes while leaving GFM tables, task
 * lists and `language-*` code fences intact.
 *
 * `u` is added back because the renderer maps it to antd's underline Text; it is not in the default
 * tag list. Anything else added here must be checked against the clickjacking case — an attacker
 * who can set `style` on any element can overlay the page same-origin, which no CSP prevents.
 */
export const sanitizeSchema = {
  ...defaultSchema,
  tagNames: [...(defaultSchema.tagNames ?? []), 'u'],
}
