/**
 * Microsoft Graph permissions for the Teams communication connector.
 * Keep docs/reference/integrations/teams.mdx aligned when these change.
 */
export const TEAMS_GRAPH_APPLICATION_PERMISSIONS = [
  {
    key: 'User.Read.All',
    permission: 'User.Read.All',
    reason:
      'Resolve tenant users by email when Wayd opens chats or writes calendar events for them.',
  },
  {
    key: 'Chat.Create',
    permission: 'Chat.Create',
    reason: 'Create Teams chats for Wayd communication through this connection.',
  },
  {
    key: 'Calendars.ReadWrite',
    permission: 'Calendars.ReadWrite',
    reason:
      'Create and update Outlook calendar events when Wayd sends scheduling-related communication.',
  },
] as const
