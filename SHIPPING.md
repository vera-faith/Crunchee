# Shipping Crunchee to TestFlight

Build 1 (1.0.0) uploaded Nov 3, 2026 — ASC app ID 6830117383, bundle com.verafaith.crunchee, team 738KBBGF8K (Vera Del Rio).

## Pipeline

1. **Unity** (Editor 6000.6.7f1, iOS Build Support installed):
   - `Crunchee → Build Butter Path Scene` (regenerates the scene, applies identity + app icon)
   - `Crunchee → Build iOS (Xcode project)` → writes `Builds/iOS/Unity-iPhone.xcodeproj`
   - Note: the Editor must be (re)started after installing the iOS module, or the build postprocess fails with "Build target 'iOS' not supported".
2. **Compliance flag**: Unity does not write `ITSAppUsesNonExemptEncryption`. Add it (`false`) to the exported `Builds/iOS/Info.plist` before archiving, or the build lands in a Missing Compliance hold.
3. **Archive** (signing deferred):

   ```
   xcodebuild -project ~/Developer/Crunchee/Builds/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination 'generic/platform=iOS' -archivePath ~/crunchee-upload/Crunchee.xcarchive archive CODE_SIGNING_ALLOWED=NO
   ```

4. **Export + upload** with `~/crunchee-upload/ExportOptions.plist`:

   ```
   xcodebuild -exportArchive -archivePath ~/crunchee-upload/Crunchee.xcarchive -exportPath /tmp/CruncheeExport -exportOptionsPlist ~/crunchee-upload/ExportOptions.plist -allowProvisioningUpdates
   ```

   Success = `Upload succeeded` in the output, then the build appears in App Store Connect under TestFlight → Build Uploads as Processing.

## Lessons (Nov 3, 2026 — five export runs to learn these)

- **ExportOptions.plist MUST contain `<key>destination</key><string>upload</string>`.** Without it, `method: app-store-connect` defaults to `destination: export`: the run prints `EXPORT SUCCEEDED` and builds the IPA locally but NEVER uploads. Apple's side shows a buildUpload stuck at AWAITING_UPLOAD with 0 files. HaloHalo's plist has the key; that is the whole difference.
- **Xcode account**: only `vfaith.delrio@gmail.com` is signed into Xcode (Admin, Vera Del Rio). The old `boogie.delrio@gmail.com` account had an expired session ("Your session has expired", keychain "missing Xcode-Token") and xcodebuild kept trying it — it was signed out on Nov 3. If uploads fail with session errors, check Xcode → Settings → Accounts first.
- `Packaging.log` only covers packaging. The real upload evidence is in the run's `*.xcdistributionlogs` folder (ContentDelivery.log) and in App Store Connect itself.
- A missing dSYM warning for UnityRuntime.framework at export is harmless.

## Accounts / identity

- ASC sign-in: vfaith.delrio@gmail.com (team Vera Del Rio, 738KBBGF8K)
- Unity PlayerSettings already carry: bundle ID, team, automatic signing, portrait-only, version 1.0.0 / build 1. Bump the build number for every new upload.
