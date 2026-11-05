# Product teams

A product records which teams and teams of teams work on it. A link says only that: there is **no
owner**, **no role**, and **no dates**. Any number of teams may be linked to a product, and a team to any
number of products. Do not describe one of several linked teams as "the owner", or say who supports a
product from these links.

## Reading them

`Products_GetTeams` lists a product's links **rolled up across everything beneath it**: a team linked to
one of its services appears on the product line too. A row whose `product` is not the product you asked
for is inherited from below it, and `productPath` says where that product sits. A team linked at more
than one level appears once per link.

`Products_GetTeamProducts` goes the other way — the products one team or team of teams is linked to. It
takes the team's **UUID**, and lists only that team's own links: a team of teams does not inherit what
its teams work on.

`Products_GetProducts` and `Products_GetProduct` carry each product's own `teams` — not rolled up.

An empty answer means nothing has been **recorded** — links are entered by hand — so say that rather
than "no team works on it".

## Linking and unlinking

Link with `Products_LinkTeam`, unlink with `Products_UnlinkTeam`. Both take the product and the team's
UUID, and both succeed without change when there is nothing to do.

- **Link the most specific product** the team works on. The read side rolls it up to every ancestor,
  and a link on a product line cannot be rolled down.
- **The team must be active** to be linked. An inactive team keeps its links and can still be unlinked.
- **Unlink from the product the link is on** — the row's `product`, which on a rolled-up row is a
  descendant, not the product you read.

### Several at once

- **One product's teams as a whole:** `Products_SetTeams` with the product and **every** team that should be
  on it, up to 100. It **unlinks any team not listed** — read the product's own `teams` from
  `Products_GetProduct` first and send them back with your change. Only the product's own links change; teams
  on the products beneath it are untouched. All or nothing: when one team cannot be linked, nothing changes,
  and the refusal names it.
- **One team's products as a whole:** `Products_SetTeamProducts` with the team's UUID and **every** product
  it should be linked to, up to 500. It **unlinks the team from any product not listed**, so read the current
  set with `Products_GetTeamProducts` first and send it back with your change; an empty list unlinks the team
  from everything. Confirm with the user before calling it with a list that drops products.

There is no history beyond the product's Activity, which records each linking and unlinking. Deleting a
product takes its links with it.
