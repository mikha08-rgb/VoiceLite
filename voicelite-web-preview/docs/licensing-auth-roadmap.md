# Licensing & Authentication Roadmap

The VoiceLite desktop app is currently distributed as a fully free build. The files and notes below prepare the codebase for reintroducing licensing and user authentication when the new backend is ready.

## New scaffolding added

- `VoiceLite/VoiceLite/Interfaces/IAuthenticationService.cs`
- `VoiceLite/VoiceLite/Services/Auth/*`
- `VoiceLite/VoiceLite/Services/Licensing/*`
- `VoiceLite/VoiceLite/LoginWindow.xaml(.cs)`
- `VoiceLite/VoiceLite/Models/UserSession.cs`

All implementations intentionally throw `NotImplementedException` (or return placeholders) so that new work does not silently fail. The upcoming licensing/auth team can replace these with concrete logic.

## Suggested next steps

1. **Define backend contracts**
   - Authentication endpoints (sign-in, refresh, sign-out)
   - License entitlements API (tier, expiry, device limits)
   - Token formats (JWT, opaque tokens) and storage rules
2. **Implement `AuthenticationService`**
   - Handle secure credential submission
   - Cache/refresh tokens via `UserSession`
   - Persist session details (encrypted at rest) if “remember me” is required
3. **Implement `LicenseService`**
   - Fetch entitlements post-authentication
   - Cache locally for offline usage; decide expiry/refresh strategy
   - Expose events/callbacks for UI updates (e.g., model gating)
4. **UI Integration**
   - Wire `LoginWindow` to trigger authentication flow
   - Add account menu/status indicator in `MainWindow`
   - Decide on onboarding flow (blocking vs. optional sign-in)
5. **Installer & Docs**
   - Update EULA + onboarding copy once licensing tiers return
   - Document upgrade paths from free build to licensed experience

Feel free to expand this file with API specs or architectural decisions as they solidify.