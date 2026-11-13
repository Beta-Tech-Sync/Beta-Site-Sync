# Release preparation review

## Scope

The second SiteSync release is in Beta-Tech-Sync/Beta-Site-Sync. The other
organisation repositories were also inspected:

| Repository | Observed state |
| --- | --- |
| Beta-Site-Sync | MAUI SiteSync, ASP.NET Core web app, and separate Kotlin Trackr sample; existing V2.0 draft |
| site-sync | Visual Studio workspace metadata only; no application source or release |
| demo-repository | Minimal HTML demonstration; recent Proof HTML checks pass; no release |

## Findings addressed

- MAUI sign-in called /api/auth/login while Identity exposes /login.
- A fresh browser could not authenticate to view the protected inventory dashboard.
- The application still advertised version 1.0.0/build 1.
- Emulator configuration referred to an unavailable Pixel_2 device profile and the
  wrong application ID, and APK selection did not require a signed package.
- Android and CodeQL YAML contained pasted fragments outside the workflow schema.
- Windows workflow used unresolved WPF template paths rather than the MAUI project.
- Docker workflow referred to a nonexistent root Dockerfile.
- Azure Maven/JAR and Gradle publish templates target projects/tasks absent here;
  copies are retained as documentation rather than executing them on releases.
- Labeler lacked its configuration file.

## Validation

The release preparation adds API regression coverage for registration, sign-in,
authenticated inventory reads, and forbidden non-admin writes. GitHub checks on
the final release commit remain authoritative for Android and Windows builds.

## Remaining release limitations

- The Kotlin sample must be validated separately from the MAUI application.
- iOS/Mac Catalyst builds and physical-device acceptance are not covered here.
- Production signing and store publication require owner-managed credentials.
- site-sync needs actual source files before it can be built or released.
- The demo repository is an organisation onboarding example, not a SiteSync build.

## Security and access observations

- Programme-Tones-Beta has Admin permission on all three repositories.
- Beta-Site-Sync and site-sync are public; demo-repository is private.
- Beta-Site-Sync's active Keep ruleset applies to all branches and blocks branch
  deletion and non-fast-forward pushes. It does not require a pull request,
  approving review, or passing status checks.
- site-sync returned no rulesets. demo-repository ruleset lookup is restricted by
  the current GitHub plan. Classic branch-protection reads are denied to this
  integration, so their status is unverified.
- Complete collaborator/team lists, deploy keys, installed app privileges,
  organisation base permissions, 2FA enforcement, and audit logs were unavailable.
- The current tracked tree had no matches for the limited private-key and common
  token patterns checked. This is not a complete history or secret-scanning audit.
- Committed SQLite databases contained zero users and zero inventory records.
  Remove these generated files and ignore local .env/database/IDE files going
  forward, so future data and credentials are not accidentally committed.
- Preserve ASP.NET Core's default trusted proxy restrictions; do not blindly
  trust forwarded headers from every sender. A remote proxy deployment needs an
  explicit trusted proxy configuration.
