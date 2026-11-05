const MAX_SUGGESTED_KEY_LENGTH = 10

/**
 * A workspace key suggested from an Azure DevOps project name: its letters and digits,
 * uppercased, starting with a letter. Empty when the name has fewer than two usable characters.
 */
export const suggestWorkspaceKey = (projectName: string): string => {
  const key = projectName
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, '')
    .replace(/^[0-9]+/, '')
    .slice(0, MAX_SUGGESTED_KEY_LENGTH)

  return key.length >= 2 ? key : ''
}

/**
 * The link to a project's work items; Wayd appends each work item's id to it.
 */
export const workItemUrlTemplate = (
  organizationUrl: string,
  projectName: string,
): string =>
  `${organizationUrl.replace(/\/+$/, '')}/${encodeURIComponent(projectName)}/_workitems/edit/`
