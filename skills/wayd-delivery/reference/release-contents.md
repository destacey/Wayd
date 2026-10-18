# Two rules that will refuse you

## A version is announced once, by one route

A release reaches its contents two ways, and may use both:

- **Packages** — the usual route, since a package is the deployment unit.
- **Versions carried directly** — for a single artifact that shipped alone, where nobody assembled a
  package.

**A version shipping inside one of the release's packages cannot also be carried directly.** Otherwise
one release announces the same shipment twice, and "what did 2026.09 contain" has two answers.

The rule is judged against what the release *ends up* containing — so moving a version out of the
direct list and into a package that ships it works, provided both changes go in the same
`Releases_SetContents` call. A manifest line that names no version record covers nothing and never
conflicts. The rule holds from the package's side too: `ReleasePackages_SetManifest` refuses a version
that a release listing the package, other than a withdrawn one, carries directly.

## A release cannot be announced while its contents have not shipped

`Releases_MarkReleased` is refused while any version or package the release carries has not shipped.
An **empty release announces normally** — a repackaging or a pricing change is announced with nothing
deployed, and emptiness is never the blocker.

To announce:

1. `Releases_GetRelease` — read the contents.
2. List every contents entry without a `releasedAt`.
3. For each, either release it (`Versions_MarkReleased` or `ReleasePackages_MarkReleased`, or a
   production deployment that succeeds) or remove it with `Releases_SetContents`.
4. Call `Releases_GetRelease` again. Repeat from step 2 until no entry lacks a `releasedAt`.
5. Confirm the announcement and its `releasedDate` with the user.
6. `Releases_MarkReleased`.
