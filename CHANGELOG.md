# Changelog

## 2.0.0

### SiteSync
- Inventory tracking with quantities, delivery status, locations, and goods-in/out pages.
- ASP.NET Core inventory API with Identity authentication and Admin-only writes.
- HTML dashboard sign-in and sign-out, with bearer tokens held only in memory.
- MAUI sign-in now uses the server's built-in /login endpoint.
- Application version metadata updated to 2.0.0 / build 2.
- Regression coverage for registration, bearer-token login, inventory reads, and denied User writes.

### Build and maintenance
- Repair malformed Android and CodeQL workflow YAML.
- Replace the unrelated WPF packaging template with a MAUI Windows build.
- Correct the standalone Docker workflow to use Web/Dockerfile.
- Correct Android emulator package checks, remove the unavailable device profile,
  enable KVM access, and select a signed APK for installation.
- Preserve unused Azure JAR and Gradle publishing templates under docs/workflow-templates.
- Add the missing labeler configuration and document the separate Trackr sample.

### Distribution notes
- SiteSync MAUI and the Kotlin Trackr sample are separate applications.
- iOS and Mac Catalyst have not been validated for this release.
- Store-ready signing is not configured by this release preparation.
