# Deployments

A deployment carries **either a version or a package, never both and never neither**. The request
schema cannot express that, so `Deployments_Start` is refused if you supply both or omit both.

**Where a package exists, deploy the package.** One pipeline run shipping fifteen services is one
deployment, not fifteen — attributing it to each component would report a deployment frequency the
organization does not have.

A version that shipped inside a package has **no deployment of its own**. Looking up its deployments
returns nothing; find the package whose manifest names it. This is the most common source of "why is
this empty?".

Outcomes are one-way: once `Deployments_Succeed` or `Deployments_Fail` is recorded, neither can be
called again, and the only step left is `Deployments_RollBack` on a success. A rollback is final.
**Failure and rollback are different**: a failure never arrived, while a rollback arrived and had to
be undone. Change failure rate counts the second kind.

**A production success releases what it shipped.** Succeeding a production deployment, or importing one
that succeeded or was rolled back, marks an unreleased version — or a package and the versions that
changed in it — Released at the deployment's completion. So where deployments are recorded, you do not
also need `Versions_MarkReleased` or `ReleasePackages_MarkReleased`. A released moment already set is
never replaced: import history oldest first, or fix it afterwards with `Versions_CorrectDates` or
`ReleasePackages_CorrectDates`.
