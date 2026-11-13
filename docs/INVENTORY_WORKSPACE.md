# Stock and signed goods workspace

## Release scope
Android and Windows are the first acceptance targets. The web workspace and browser demo use the same InventoryUi source. Both Kotlin app variants can open Stock & deliveries.

## Daily workflow
1. Add your own item, or import a CSV template / JSON backup. The 72 catalogue definitions start with zero counted stock.
2. Enter item codes, categories, units, barcode/NFC IDs, opening quantities and reorder levels.
3. Scan a QR/barcode or compatible NFC tag, or use keyboard/manual lookup.
4. Receive or issue actual quantities against a delivery/job reference. Capture condition, named acknowledgement, signature, goods time, recorded time and a handover location. GPS is optional and requested only when used.
5. Export a stock CSV snapshot, a full JSON backup, or a readable signed receipt. Use the device/browser share sheet for supported communication apps.

Damaged and quarantined receipts do not increase available stock. Signed records cannot be edited/deleted through the app; add correcting records. They are application acknowledgements, not cryptographically verified identities or safety certificates.

## Storage and portability
- Web: authenticated clients manage their own account-scoped inventory.
- MAUI/Kotlin: private local device workspace; existing local inventory is imported on first use. The v2 workspace then owns stock updates. Legacy task data remains separate.
- Demo: this browser only; back up before clearing browser data.
- Cross-device/team cloud synchronisation is not implemented. Transfer is explicit via exported files.
- CSV is a stock snapshot without signatures; JSON preserves the versioned workspace and signed records. Imports add new codes and preserve existing definitions.
- Built-in phone NFC supports compatible near-field tags. UHF/long-range RFID needs external hardware; keyboard-mode readers can enter codes.

## Extension points
InventoryUi/core.js owns schema-v1 validation, balances, CSV and merge rules. InventoryUi/app.js uses replaceable storage/device adapters. Workspace/WorkspaceDocument.cs validates server/native .NET writes. Kotlin hosts packaged content with restricted navigation and native file/reader adapters. Preserve schema and migration compatibility when adding providers.

## Validation
9 shared-rule tests and 8 API integration tests pass locally. Native builds, hardware scanning, GPS, signature interaction and OS sharing still require device acceptance. GitHub validation results on the exact release commit are authoritative. Store signing and publication remain separate steps.
