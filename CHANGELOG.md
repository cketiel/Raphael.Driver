# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
The record starts at version `1.1.0`; earlier history is not reconstructed.

## [Unreleased]

### Fixed

- A refusal from the server is no longer taken for "no coverage". When the server answers that a
  Pull-out or Pull-in has no inspection for this driver, the app no longer opens the checklist kept
  on the phone as a required inspection, which blocked the Pull-out with an inspection the server
  then refused. The checklist on the phone is used only without coverage (RE-067).
- Every refused inspection says why: the server's own reason, or what the answer means (route you
  cannot drive, session expired, photo too large, server problem). The generic "The server did not
  accept the inspection" is gone (RE-067).

## [2.0.0] - 2026-11-07

### Changed (RE-066 — navigation)
- Native bottom bar and tabs: main pages and Today, History and Future are built once and reused.
- Pages are prepared at the sign-in screen; Appearance is built when it first opens.
- Settings › Account has Contact, Messages, Change route and Sign out.
- The GPS service no longer stops the app when Android refuses to start it in the background.

### Fixed (RE-066 — speed)
- Route tracking now runs as a real foreground service: Android shows the location notice while a route is in progress and no longer stops the GPS in the background.
- The page keeps its height when the bar above changes after a dialog or the keyboard.
- The bottom menu no longer shows cut off or stretched when changing section: pages keep the height of the visible window area.
- Internal pages open without animation and the next one is prepared in advance; returning to Today redraws only the rows that changed.
- The status bar no longer turns red when the app is reopened.
- The menu changes page at once: no wait of 440 ms for its animation, no page transition, and the
  full menu and its dialogs open without delays or fades.

### Changed (RE-066 — Google Play)
- Targets Android 16 (API 36) on .NET 10, as Google Play requires. CommunityToolkit.Maui 14.
- The stamp's wait explanation opens centered: the toolkit no longer anchors popups.

### Security (RE-066)
- `allowBackup=false`: patient data is not copied to Android backups. A reinstall signs in again.
- Removed the `QUERY_ALL_PACKAGES` permission.

⚠️ **RE-062 needs the Raphael.Backend release with RE-062.** Against an older backend the inspection
cannot be sent and the outbox keeps it; switch the inspection on for a Provider only once its drivers
have this version.

### Added (RE-065 — compliance documents)

- Settings › MY DOCUMENTS (also in All options): the driver's documents and those of the vehicle of
  their route, each with its state (valid, expiring, expired, in review, rejected with the reason).
- Upload or renew a document with a photo, an image from the gallery or a PDF, and its expiration date.
  The five vehicle photos are guided step by step with the outline of each side.
- A banner on the Dashboard when a document expires within a week, has expired or was rejected.
- The office's message about documents (sent from the Desktop) stays on the Dashboard until read;
  tapping it shows it whole, with a way into My documents.
- Angel and Metro menus: the tab in use in the accent colour in Dark and Infrared, where it barely
  stood out.
- My documents redesigned: a header with the driver and three counters (valid, need you, in review),
  tabs with a badge, and cards with a stripe in the colour of their state, the kind of file (PDF,
  image, 5 photos), the expiry date and a bar of the year left.
- Settings reorganised: the driver, username and vehicle at the top, then three tabs — Account (my
  documents, change password, version), Appearance (theme, schedule and menu styles) and Navigation
  (default maps app).
- Change password from Settings › Account, with the current one, in a sheet from the bottom.
- One style of tabs in the whole app (the schedule's History · Today · Future), now also in My documents
  and Settings. No red backgrounds on tabs, headers or actions: red is kept for errors.
- My documents: the counters say whose they are (My documents or the vehicle's); View and Upload / Renew
  have icons and are centred; View shows "Opening…" while the file comes, and an upload covers the page
  with its progress ("Uploading photo 2 of 5…").

⚠️ Needs the Raphael.Backend release with RE-065.

### Added (RE-064 — menu styles)
- A menu at the bottom of Dashboard, Schedule, Notifications, Settings and Contact, with Home,
  Schedule, Alerts and Settings always in view. It reaches the same pages and runs the same
  commands as the side menu did.
- Sixteen menu styles in the code (General, Thematic, Neutral, Christmas); three are offered.
- All options: the logo with its halo, MAIN and MORE in two columns (icon and name), and Sign out.
- Sign out asks first: "Sign out?", Cancel or Sign out.
- Schedule opens Today with History · Today · Future tabs; the route icon beside them is Change
  route. VIEW HISTORY and CHANGE ROUTE are gone.
- Alerts in the menu shows the unread count, live, as notifications arrive or are read.
- All options › MORE has Change route and Call me; the Angel's wings open with Messages, Call me (in
  Contact's place: drivers use it more), Change route, Sign out and All options.
- Call me sits in the title bar, as a round icon in the bell's old place, instead of floating over
  the cards. Its colour still says how the request is going.
- Settings › MENU STYLE offers three styles as cards with a picture of each: Metro (the default),
  Underline (logo on the right, by the thumb) and Crescent. The other styles, Angel among them, stay in
  the code and are not offered; a phone that had one goes to Metro.

### Changed (RE-064)
- No side menu: the hamburger is gone, the menu at the bottom replaces it.
- No bell in the title bar of the pages with the menu: Alerts in the menu replaces it.
- The official logo has the angel's halo everywhere: side menu, sign-in, launcher icon and the
  system launch frame (red version in Infrared).
- Settings scrolls.

### Added (RE-062 — vehicle inspection and working without coverage)
- Vehicle inspection inside the Pull-out and the Pull-in, when the driver's Provider has it on: odometer
  (miles), VIN (confirm the one on file, or photograph and type the one on the vehicle), the checklist with
  a camera photo for every problem, a comment and the driver's signature. Without it, Perform is not
  offered. At Pull-in its signature replaces the end-of-shift one. With the module off, nothing changes.
- The inspection screen: a header with the vehicle and the progress, a card per step and per checklist
  group with its count, OK/Problem as a segmented control, the photo and the signature shown in place.
- Working without coverage: every change the driver makes (stops, signatures, Start Trip, cancellations,
  phone numbers, notes, notification marks, the office contact, inspections with their photos) is kept
  on the phone when it cannot be sent and sent, in order, as soon as the connection is back. The day's
  schedule and the active run are read from the phone's copy, with the pending changes on top; GPS fixes
  are kept and sent in batches. A notice says when something was kept. Not kept, on purpose: sign-in,
  Call me, the SMS to the patient, push-token registration and ETA updates.
- **Map Pin is the default schedule style** for a phone whose driver has not picked one, and it is
  reworked: the whole card border in the stop's colour, thin; the space type and funding source on a
  header band washed in that colour (as Tint does); the promised time framed as Ticket frames it, beside
  the coloured calendar; and on the stop screen the next thing to do washed in the stop's colour.
- The VIN on file shows its last 4 characters large and in the accent colour: those are what the
  driver checks on the vehicle.
- The stop's notes screen, redrawn from the designer's V7 proposal: a header with the place, the note the
  driver reads first, one card per kind of note (this trip, entrance, place, patient, the file) with its
  icon, count and its own add button, and each note on a tinted panel with its source, a *To review*
  badge and Edit / Confirm / Delete.
- Start Trip sends the vehicle's position and the moment it was pressed, so the server re-times the
  pickup from where the vehicle really is.
- Camera permission (Android and iOS).

### Changed (RE-063)
- CHANGE ROUTE to a route of another company says it is not available and goes back, and the phone's
  copy of that route is deleted (needs the RE-063 backend).

### Fixed (RE-062)
- Inspection: tapping OK did not turn the line green until something else redrew the screen (for
  example coming back from the camera). The OK and Problem segments now swap between their selected and
  plain looks instead of being recoloured.

### Removed (RE-062)
- The INSPECTIONS item of the side menu, which opened Samsara Driver.

### Added (RE-061 — V7 skin)
- Four themes (Light, Gray, Dark, Infrared) from the V7 design tokens, chosen with a four-dot
  switch on the sign-in page and in Settings, and kept on the phone. Until a driver picks one the
  app follows the phone's light/dark setting, as before. Settings also has "Use phone setting".
- Animated splash, design Q "all the trips". The system's launch frame (Android's splash, iOS's
  launch screen) shows the official logo centred on a pale street-map colour. The splash then
  draws a street map; the day's trips appear around it as small pins, fly to the centre and melt
  into the logo, which beats in with its map shadow; then RAPHAEL / DRIVER and "Driving Health /
  The Rider's Angel" come in. 2.7 s, the design's own timing, counted from the first frame really
  drawn, so a slow phone does not lose the pins. A tap skips it. The splash looks the same in
  every theme. With the phone's animations turned off (Reduce Motion on iOS) it shows the
  finished lockup and goes straight on.
- Schedule styles: eleven ways to draw the events of Today's Schedule and their stop screens,
  chosen in Settings → SCHEDULE STYLE (a strip of real cards, shrunk; a tap previews one and
  "Use this style" takes it) and kept on the phone. Token, Folded Corner, Tint, Ticket, Road
  Signs, Transit Line, Outline, Map Pin, Split Pill, Flap Board and Legacy, the old look, which
  stays the default until a driver picks another. All keep the card's size and the convention
  colours, and tell pickup from dropoff and Appt from Return without the colour too (arrows,
  destinations, solid or striped, filled or hollow, shapes).
  - The promised time (pickup time or appointment) carries AM/PM and its label, and is set off
    from the ETA so the two cannot be taken for each other.
  - The space type (AMB, WCH, STR) shows its icon: a person walking, a wheelchair, a stretcher.
  - A pickup with a planned wait shows a stamp with the minutes over the empty right of the
    card. A tap on the stamp explains it and opens nothing; a tap anywhere else opens the stop,
    as before. The wait was already in the schedule (ScheduleDto.Wait); nothing new is asked of
    the server.
  - On the stop and garage screens the card that was tapped stays on top, the wait is written
    out under it, and the actions are set off the way the style sets off its card. Same actions,
    same order.
  No help topic: the Driver has no help yet.

### Changed (RE-061 — V7 skin)
- The event screen is titled STOP (it was EVENT DETAILS) and the pull-out and pull-in screen
  GARAGE (it was the event's name).
- Raphael brand colours in place of the maroon: blue top and status bar, coral main buttons,
  blue secondary buttons. Every screen now follows the theme, including Dashboard, History,
  Notifications and Settings, which were dark whatever the phone was set to.
- Sign-in page redesigned: picture of the van on top, a sheet with the logo, the fields, Remember
  me and Sign In. "MILANES System" and "by Milanes Service LLC" removed. The version line still
  opens the server dialog after seven taps.
- Sign-in: the official logo sits centred on the seam between the picture and the sheet, half
  on each, with the name RAPHAEL over DRIVER centred under it. "Welcome!" removed, so Sign In
  shows without scrolling.
- "Call me" uses the V7 colours for its four states.
- Event cards keep their colour per event type; only their background and text follow the theme.
- The signature pad stays white with black ink in every theme, because that image is what is saved.
- A disabled button is now its own colour at half opacity instead of grey.
- Side menu: the head on the V7 gradient with the logo in its own colours, on a white tile shaped
  like a phone app icon (a squircle) so it stands out from the gradient; rounded highlight on the
  page on screen; icons in the accent colour, the selected one in coral.
- In the Infrared theme the logo is drawn in the theme's reds (menu, sign-in, Schedule), so
  nothing blue breaks the night mode.
- The menu and back buttons of the top bar sit on a rounded square (Android's own icons, at their
  own size), and the status bar is a touch darker than the top bar, leaving the faint seam
  between the two that V7 shows.
- New official logo: the V7 vector replaces the old raster logo everywhere in the app and in the
  launcher icon (white background, logo in front).
- The official logo carries the map shadow under the tip and no halo (decided 2026-10-31). It is
  the logo of the side menu, the sign-in page, the launcher icon and the launch frame (in the
  Infrared theme, the menu's and sign-in's in reds). Schedule keeps the logo without the shadow.
- Settings: rounded cards for the theme and for each map app, the app icon on a rounded tile;
  the chosen map shows an accent border and tint and an accent dot (it was always red).
- Dashboard redesigned: greeting, Run Info on tiles, and Today's Events with the count, one bar
  segment per event of the day and the next event, which opens it directly. It is the first
  pending event, opened exactly as Today opens it, so the order of the day is unchanged. The
  Dashboard also resends trip starts made without signal (RE-057), as Today does.
- Signing in lands on the Dashboard instead of the Schedule page.
- Icons on SCHEDULE, TODAY'S SCHEDULE and FUTURE SCHEDULE. SCHEDULE opens the Schedule page
  when the driver has no run of their own today, instead of doing nothing.

### Fixed (RE-061 — V7 skin)
- The top bar was MAUI's default navy, never the maroon the app asked for: the style that set it
  never reached the Shell. It now takes the theme's colour on every page.
- Following the phone's light/dark setting now repaints the app while it is open.

## [1.7.4] - 2026-10-26

**No slice.** versionCode 18. Same code as 1.7.3; it exists to bring the phones back to the
production server after the MyASP contingency (`_meta/MYASP_FALLBACK_RUNBOOK.md`). The contingency
APK `1.7.3-myasp` used versionCode 17, so returning needs a higher one: Android does not install a
lower versionCode over an installed app. Installs over `1.7.3-myasp` and over `1.7.3` without
uninstalling. Needs Raphael.Backend 2.4.3.

### Changed
- Version 1.7.4, versionCode 18. Connects to the production server (`api.raphaeldh.com`).

## [1.7.3] - 2026-10-18

**Slices:** RE-054, RE-055, RE-057, RE-059 (four, so the patch is .3). versionCode 16. **Needs
Raphael.Backend 2.4.3** for the notes and for Start Trip to reach the office and the patient; against
an older backend the app works as before.

### Added (RE-059 — local test environment)
- A `Local` environment (`http://10.0.2.2:5241/`, the API on the developer's PC seen from the emulator).
  Plain http is allowed **only** in a build made with `RaphaelApiEnvironment=Local`; this release is
  `Prod` and keeps https only.

### Fixed (RE-055)
- **The login failed dialog said "Invalid credentials" whatever the server answered.** It now shows the
  reason, as the text under the form already did (e.g. "Your company's access is disabled").


⚠️ **RE-057 needs Raphael.Backend 2.4.3** (start trip). Against an older backend Start
Trip still opens the map and Arrive works; only the office and the patient are not told.

### Added (RE-057 — start trip)
- **Start Trip.** On the next pickup, before Arrive, the only step offered is Start Trip: it opens the
  map, as Maps did, and tells the office and the patient that the driver is on the way (with the travel
  time the route already carries). Then Arrive appears, and Maps is back to reopen the navigation.
- **Never blocking**: without signal or with a server error the start is kept on the phone, Arrive is
  offered anyway and the server is told when it answers. It survives closing the app: on reopening,
  a start the server does not show yet is sent again.
- **In order, like every other step**: Start Trip is only offered on the first pending event.
- Dropoffs work as before.

### Changed (RE-055)
- **A refused sign-in says why.** When the server answers 403 (the account or the driver's company was
  switched off), the login screen says so instead of "Invalid credentials".

⚠️ **Needs Raphael.Backend 2.4.3** (places and notes). Against an older backend the
notes page answers that the notes could not be saved; everything else works as before.

### Added (RE-054 — notes for the driver)
- **Stop notes page.** Tapping NOTES on a stop's detail opens it: what the driver reads at that stop,
  this trip's own note, the entrance's and the place's notes, the patient's notes, and what the clinic
  or the file sent. The driver adds, edits, confirms and deletes notes there, saved at once; a note of
  the place or the patient reaches every trip still to come. The big ADD NOTE button stays at the bottom.
- **No connection:** the driver is told and nothing is kept on the phone to send later (decided
  2026-10-18).
- Back on the stop's detail, the notes show what the driver now reads.

## [1.6.0] - 2026-09-24

⚠️ **Needs Raphael.Backend 1.2.0 (RE-026), and Desktop 1.10.0 on every office workstation first.**
Against an older backend "Call me" cannot send and offers to call the office instead. Published as
the tested candidate rc.2 (versionCode 15), renamed, not rebuilt.

### Added
- **"Call me".** A floating button on the dashboard, the schedule screens, the event detail and the
  Pull-out detail - never on the signature page - asks dispatch to call the driver back instead of
  the driver phoning in. It shows how the request is going: blue, orange with the minutes waited,
  green when somebody in the office has it, red after a missed call with "I can talk now". Remind,
  cancel, and a toast for every answer.
- When a request cannot be sent, "Call the office" dials the office's number, kept on the phone so
  it works with no data.
- The request carries the driver's line, remembered from the last screen that knew it, and a fresh
  GPS position when tracking is on.
- A one-time explanation of the button on Today's Schedule.

### Changed
- Contact's "Call the Office" and the event's "Call Dispatch" are disabled, not removed: same place,
  greyed out, pointing to "Call me". The order Arrive, Signature, Perform/Cancel does not change.

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
