# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
The record starts at version `1.1.0`; earlier history is not reconstructed.

## [1.5.0] - 2026-09-20

⚠️ **This is the version the Azure cutover starts from.** Until now the address of the server was
compiled into the APK, so moving the backend meant visiting all 31 phones - and that is what
stalled the cutover. Session renewal needs a backend that serves `POST /api/Auth/refresh`
(Raphael.Backend 1.1.0 or later); against an older one the sign-in response simply carries no
refresh token and the application behaves exactly as it used to.

### Added
- **The phone can be pointed at a server without a new APK.** Seven taps on the version label of
  the sign-in screen open a dialog: Production, Development, a typed address, or back to the
  built-in default. It has to live on the sign-in screen, because Settings is behind the sign-in
  and signing in is the thing that needs an address. Seven taps rather than a button so a driver
  never finds it by accident and support can describe it over the telephone. Moving the whole
  fleet is still a DNS change: the compiled-in value is the custom domain, never the Azure host.
- The flyout and the sign-in screen name the server when it is not production. The sign-in screen
  is seen once at the start of a shift; the flyout is open all day, and a phone left on
  development otherwise looks like every other one.
- The version label reads `1.5.0 (13)`, display version and build number together. Two builds of
  the same version looked identical on screen while behaving differently.
- **The session renews itself**, so a driver is not sent back to the sign-in screen in the middle
  of a route. Renewals are serialised, and a renewal that fails because the network is down does
  not end the session.
- Every request identifies the application and its version to the server (`X-Client-App`,
  `X-Client-Version`), sign-in included - which is also what finally puts the sign-in call inside
  the server's telemetry.

### Changed
- **On a return leg the five-minute early-arrival margin is now used**, not fifteen. Merged before
  1.4.0 was cut and never shipped until now: until this APK, driver and dispatcher were reading
  hours ten minutes apart on every return trip.
- Signing in opens today's schedule straight away and brings notifications up behind it. None of
  that bring-up is needed to read a schedule.
- The one log line that says why a session ended survives in the Release APK (`RAPHAEL-SESSION` in
  logcat). Every other diagnostic in this app is compiled out of Release, which is why the last
  three faults each cost a build to find.
- The schedule DTO carries the waiting time the server derives for an early arrival. Nothing draws
  it yet; the copy is kept in step with `Raphael.Shared`, which is the source of truth.

### Fixed
- Signing in used to wait for the entire notification bring-up before showing anything: an HTTP
  round trip, a SignalR connection, the Android permission dialog and a Firebase token. On a fresh
  install that was tens of seconds of spinner, and if the permission dialog went unnoticed it never
  finished at all.
- Signing out froze the phone, every time, with no way out but killing the application.

## [1.4.0] - 2026-08-29

### Changed
- **The Google Maps key is no longer inside the application.** Travel times come from Raphael.Api,
  which serves what it already knows and buys from Google only what nobody has asked for yet. Until
  now the key travelled inside every distributed APK, where anyone who unpacked one could read it.
- Recalculating the arrival times of the next stops asks for both legs in a single request instead
  of one after the other.

## [1.3.0] - 2026-08-27

### Added
- Every page title carries the icon it already had in the side menu.
- A route change now shows up in the bell like any other notification, so the driver can see
  what moved their schedule instead of only watching it move.

### Changed
- Future Schedule shows the **next day only**. It used to list every day ahead in a single
  list, with several Pull-outs and several Pull-ins in it and nothing to tell the days apart.
- A route change only interrupts a screen that was already open when it arrived, and only
  while it is less than an hour old. Every screen loads current data as it opens: being asked
  to reload what you have just loaded is noise.
- Settings uses the same title bar as every other page, bell included.

### Fixed
- The route-change overlay and its countdown never appeared. The schedule reloaded five
  seconds later with nothing on screen to explain why: the overlay was built off the UI
  thread and the failure only reached the debug log.
- Future Schedule was a dead list. No event could be opened, so the calls and texts added in
  1.2.0 could never be reached. An event opens now and offers exactly two actions: call the
  patient and text them.
- The bell was missing from Today's Schedule and Future Schedule - the two screens where a
  driver spends the shift, and so the two where a new notification went unnoticed.
- Page titles sat right of centre. Android places the title view after the navigation icon,
  so a title centred inside it is not centred on the screen.
- The "no trips" message is centred on both schedule screens.

## [1.2.0] - 2026-08-27

### Added
- Notifications reach the driver at last. The backend had been producing them for a while,
  but the app had no way to receive them: it now has an inbox, a live channel and push.
- Bell with an unread counter in the navigation bar, and a NOTIFICATIONS entry in the menu.
- Notifications screen: pull to refresh, mark as read and unread, mark all as read, and hide
  a notification. Hiding is local to the phone — the record stays on the server and is removed
  by the retention policy, never by the app.
- Push notifications through Firebase. Tapping one opens the app on the notifications screen.
- The app reacts to route changes while the driver is working: when a trip is added to the
  route, taken off it, or cancelled, the schedule on screen is corrected. The driver is told
  what happened and why the screen is about to change.
- Drivers can now call and text the patient from a future trip's detail.

### Changed
- Page titles are centred.
- Push notifications show the Raphael icon instead of a blank square.

### Fixed
- ETA calculation no longer schedules a driver to reach a pickup more than fifteen minutes
  early, which is the limit the business rule allows.
- Signing out closes the side menu instead of leaving it open over the login screen.
- "Copy phone number" copied the address.
- Signing out now unregisters the device, so the next driver to use the same phone does not
  receive the previous one's notifications.

## [1.1.0] - 2026-08-11

### Added
- The application version is shown on screen.

### Changed
- Flyout menu styles were standardised.
