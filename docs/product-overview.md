# Kitchain

**Your pickleball companion.**

Kitchain is a pickleball companion platform with an initial focus on Metro Manila, Philippines. The long-term player journey is **Discover → Book → Queue → Play → Score → Track**, with Gear discovery alongside it.

## Product pillars

- **Courts — Where can I play?** Explore now lists API-backed venues with city, setting, court-count, price and single-amenity filters plus server pagination. The backend also supports read-only details; the full details UI, normalized availability and booking-provider integrations remain future work. Community court contributions, verification and moderation will be integrated here.
- **Play — Let's play / manage the game.** Future guest-friendly sessions, fair open-play rotation, queueing and scoring. Queue and scoring belong inside sessions.
- **Gear — What should I play with?** Future paddle discovery and comparisons. The signature PaddleMatch recommendation feature belongs inside Gear and must explain its recommendations.

## Principles

- **One-click-first:** minimize interactions for common actions.
- **Defaults before customization:** offer sensible defaults; keep advanced settings out of normal flows.
- **Fair by default:** prioritize fair player rotation when open play is implemented.
- **Guest-friendly:** joining a future Play session should not require an account.
- **Accounts provide real value:** introduce them for persistence, history and personalization.
- **Consolidate before replacing:** start with availability aggregation and external booking redirects, not payments or booking processing.
- **Community contribution:** incorporate contributions and moderation into the relevant modules.
- **Explainability:** PaddleMatch should give understandable reasons for recommendations.
- **Clear provenance:** distinguish manufacturer specifications, subjective Kitchain ratings, community data, internal statistics and external sources.
- **Incremental delivery:** implement capabilities only when their phase begins.

## Navigation and visual direction

Desktop uses a Kitchain wordmark with Courts, Play and Gear. Mobile uses Home, Courts, Play and Gear in bottom navigation. Future profile access belongs in the header; admin is outside normal navigation. There is no standalone Community or PaddleMatch navigation item.

The provisional direction combines warm racquet-club and contemporary sportswear influences: forest, ivory and restrained clay, an editorial display face used selectively, and a readable sans-serif UI. Colors and typography are starting points, not a final brand system or logo.

## Current implementation

Phase 2A adds venue persistence and a public read-only Courts API. A Court is a facility, not a playable slot. Records include location, physical court count, Indoor/Outdoor/Mixed classification, normalized amenities, approximate starting prices, external booking metadata, and owner/community/curated provenance. Only Published records are discoverable; Draft and Inactive records are hidden.

Phase 2B adds a warm editorial Courts Explore page with abstract court geometry, URL-backed searches, collapsible mobile filters and honest loading/empty/error states. Venue rows show actual API fields, approximate prices with their units, booking method and provenance. View court opens a small details placeholder until Phase 2C; it does not pretend to book or display live schedules.

The optional development set contains seven explicitly fictional venues, five published. No actual businesses or live schedules are represented. Availability is reported as `NotIntegrated` and shown as “Availability not connected yet,” never inferred to be unavailable. Home, Play and Gear remain introductory. Sessions, recommendations, accounts, availability synchronization, submissions and moderation are not implemented.
