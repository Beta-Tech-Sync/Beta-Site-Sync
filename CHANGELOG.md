# Changelog

## 2.0.0

### Client stock and goods workflow
- Add 72 building-supply and consumable definitions plus easy custom client stock.
- Add CSV/JSON import/export, backups, native/browser sharing and readable receipt exports.
- Add signed receipt/issue records with condition, timestamps, handover location and optional GPS.
- Add barcode/QR and compatible NFC device adapters; keep manual/keyboard-reader lookup.
- Isolate web stock by signed-in client, reject stale updates and preserve signed records.
- Share the workflow across MAUI Android/Windows, Kotlin app variants and the browser demo.
- Keep native and demo storage explicitly local; do not claim automatic cloud sync.

### SiteSync
- Inventory tracking with quantities, delivery status, locations, and goods-in/out pages.
- ASP.NET Core inventory API with Identity authentication and Admin-only writes.
- HTML dashboard sign-in and sign-out, with bearer tokens held only in memory.
- MAUI sign-in now uses the server's built-in /login endpoint.
- Label example dashboard content and local storage accurately; remove the unimplemented scan claim.
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

- Align the Kotlin sample minimum SDK to 24 (Android 7.0) for its Navigation dependency.

### Distribution notes
- SiteSync MAUI and the Kotlin Trackr sample are separate applications.
- iOS and Mac Catalyst have not been validated for this release.
- Store-ready signing is not configured by this release preparation.
