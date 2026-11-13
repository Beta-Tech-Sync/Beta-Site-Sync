# SiteSync Tracker

SiteSync helps track goods, quantities, delivery status, and locations with a
.NET MAUI application and an ASP.NET Core inventory API and HTML dashboard.

## Repository layout

| Path | Component |
| --- | --- |
| SiteSyncTracker.csproj, Views, Services | SiteSync MAUI application |
| Web | ASP.NET Core API and browser dashboard |
| Web.Tests | API integration tests |
| app, app-compose, shared | Separate Kotlin Trackr Android sample |

The Trackr sample's original accessibility documentation and attribution are
preserved in [docs/Trackr.md](docs/Trackr.md). It uses different application IDs
from SiteSync and is built by the Android/Gradle workflows.

## Run the web application

Install the .NET 10 SDK selected by global.json.

1. Set Admin__Email and Admin__Password in your environment. Use a strong password
   of at least 12 characters including a number and a symbol.
2. Run: dotnet run --project Web/SiteSyncTracker.Web.csproj
3. Open the URL printed by ASP.NET Core and sign in with the configured administrator.

The dashboard keeps its bearer token in memory; refresh requires signing in again.
Registered User accounts may read inventory; Admin accounts may also create,
update, and delete inventory through the API.

Use HTTPS outside a local development environment. The MAUI RemoteSyncService
uses the built-in POST /login endpoint. Registration is available at
POST /api/auth/register; authenticated inventory is at /api/inventory.

### Docker

Copy .env.example to .env, set ADMIN_EMAIL and ADMIN_PASSWORD, then run:

    docker compose up --build -d

Open http://localhost:8080 for local use. The compose service persists databases in
the sitesync-data volume. Put a trusted HTTPS reverse proxy in front of any remote
deployment; the compose HTTP port is intended for local development.

## Build and verify

    dotnet test Web.Tests/SiteSyncTracker.Web.Tests.csproj -c Release
    dotnet build Web/SiteSyncTracker.Web.csproj -c Release
    dotnet workload install maui-android
    dotnet publish SiteSyncTracker.csproj -c Release -f net10.0-android -p:TargetFrameworks=net10.0-android

Build Windows on Windows with the maui-windows workload and framework
net10.0-windows10.0.19041.0. iOS and Mac Catalyst require an appropriate macOS/Xcode
environment and are not validated by these release checks.

For the separate Kotlin sample, import the root Gradle project in Android Studio,
or run ./gradlew assembleDebug testDebugUnitTest. Commit the Gradle wrapper JAR
alongside its properties and scripts so clean checkouts can build.

## Release 2.0

See [CHANGELOG.md](CHANGELOG.md) for changes and
[docs/RELEASE_REVIEW.md](docs/RELEASE_REVIEW.md) for validation and limitations.
Development APKs are not Play Store-signed distribution packages. Production
Android and Windows distribution require the owner's signing and store setup.

## License and attribution

See [LICENSE](LICENSE). The Kotlin Trackr sample includes Google/Android Open
Source Project code and retains its existing Apache 2.0 notices.
