# Gear G1 — data and ingestion foundation

G1 adds backend models, persistence, reviewed ingestion and public reads. It does
not add a catalog UI, scraping, AI calls, automated prices, authentication or Play
integration. The existing Angular `/gear` foundation placeholder is unchanged.

## Boundaries and units

| Concept | Representation | Authority |
| --- | --- | --- |
| Brand | `GearBrand` | Stable identity, normalized name, unique slug, optional website |
| Model | `Paddle` | Brand-scoped slug, editorial metadata, publishing state |
| Variant | `PaddleVariant` | Model-scoped slug/SKU, publishing state |
| Facts and measurements | `PaddleSpecificationEvidence` + owned `GearSpecifications` | Source, claim type, observation timestamp for every non-null value |
| Provenance | `GearDataSource` | Source type, reference URL/domain, retrieved/last-checked timestamps |
| Commercial observations | `PaddleListing` | Variant, seller/source, currency, price, stock and checked time |
| Imagery | `PaddleImage` | Variant, reference URL, optional source, ordered display |
| Kitchain interpretation | `PaddlePerformanceProfile` | Nullable 1–10 scores, method version, independent publishing state |
| Ingestion review | `GearImportCandidate` | Validated proposal, source, decision and matched variant audit |

Thickness is in millimeters. Paddle dimensions, handle length and grip
circumference are inches. Advertised minimum/maximum and measured static weight
are ounces. Swing/twist weight are moments of inertia in kg·cm². All are nullable
`decimal(10,3)` and strictly positive when supplied. Ingestion must normalize units
before submitting them; unknown is null, not zero. Excess precision is rejected,
not silently rounded by the server. Price uses `decimal(12,2)`, may be null or zero,
and must be non-negative. Currency is a three-letter uppercase code syntax, not a
validated live ISO currency registry. Timestamps are UTC; PostgreSQL retains
microseconds.

Thickness and shape are variant-scoped **evidence**, not duplicated identity
fields on the variant. A variant's name/slug/SKU identifies the configuration;
the source-backed assertions describe it. Materials and construction remain
bounded open text because an enum would prematurely constrain them. Product
descriptions are intended for original Kitchain editorial text, not copied pages.

## Evidence and source conflicts

One evidence row holds any subset of typed specs sharing one source, evidence
type and observation time. Every non-null field is attributable through that
row. Different sources, claim types or observation times require separate rows.
This preserves field-level provenance without an arbitrary string-valued EAV
schema. Claimed weight ranges and measured static weight have distinct fields.

For example, fictional test evidence can retain both a manufacturer-stated
16 mm thickness/8.0–8.3 oz range and an independent 15.9 mm/8.27 oz observation.
Neither overwrites the other. These are synthetic test values, not real products.
The public detail response returns all evidence with source and observation time;
there is no preferred-value cache or inferred confidence score.

G2/G3 can implement a per-field display policy: manufacturer claims for marketed
dimensions, identified independent measurements for measured properties, freshness
within each source/type, and an explicit conflict indicator. Always retain the
chosen evidence ID and alternatives. "KitchainVerified" is a curator assertion,
not an automated verification result. Performance ratings are separately named
`kitchainPerformance`, have a method version, and never replace raw evidence.

## Review and matching

External source → future extractor → normalized version-1 DTO → server/domain
validation → Pending candidate → explicit review → canonical catalog.

No network fetch happens when a URL is registered. One candidate is one review
unit. G1 deliberately has no batch entity, raw JSONB payload, HTML storage, bulk
approval or editable candidate. The typed proposal remains stored after review.
Reject and resubmit corrections. Candidate inspection plus its source reference
provides the initial review record; reviewer identity requires future auth.

Approval with `{}` creates a published variant and evidence, creating the brand
and model when absent. Brand name keys collapse whitespace and use invariant
uppercase; slugs normalize to lowercase. A matching brand must agree on both
name and slug. A matching model must agree on normalized name and be Published.
Variant slug and normalized SKU are unique **within a model**, not globally.
Same display names on different models/configurations remain possible.

Approval with `{"targetVariantId":"...","notes":"..."}` explicitly attaches
new evidence to an existing Published variant of a Published model. The reviewer
is responsible for the identity match. The proposal's extracted name can differ;
it remains in the candidate audit. Existing identity, metadata, performance and
evidence are never overwritten. A duplicate variant does not auto-merge: it
returns 409 and remains Pending so a reviewer can explicitly match it.

Review locks the candidate row (`FOR UPDATE`) in a ReadCommitted transaction.
Matching, canonical inserts, audit decision and save commit together. Concurrent
decisions on the same candidate produce one success and one conflict. Database
identity constraints handle competing imports of the same brand/model/variant;
the losing request rolls back and returns 409 for review/retry. Approvals and
rejections are final. Missing references return 404, invalid proposals 400, and
database unavailability a sanitized 503. After an uncertain network/503 result,
reload the candidate before retrying.

Future matching can add brand aliases and model/SKU suggestions, followed by human
confirmation. Do not normalize similar-looking names into automatic identity
merges or let an extractor publish without review.

## APIs

All request enums use the existing string-enum JSON convention. Unknown request
properties are rejected. Candidate requests are limited to 32 KiB, source requests
to 8 KiB by the host request-size limit. Typed field lengths and numeric bounds
are enforced separately. TestServer does not implement host request-size limits;
deployment hosting must honor them. No raw unvalidated payload drives EF writes.

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/gear/paddles?offset=0` | 25 models at most, stable name/ID order, brand and published variants/primary images |
| GET | `/api/gear/paddles/{id}` | Published model/variants, all source-backed specs, ordered images, checked listings, published interpretation |
| POST | `/api/admin/gear/sources` | Register source; 201 |
| GET | `/api/admin/gear/sources?offset=0` | 50 sources at most, name/ID order |
| POST | `/api/admin/gear/imports` | Create validated Pending proposal; 201 with Location |
| GET | `/api/admin/gear/imports?status=Pending&offset=0` | 50 proposals at most, oldest first, then ID |
| GET | `/api/admin/gear/imports/{id}` | Full typed proposal and review audit |
| POST | `/api/admin/gear/imports/{id}/approve` | New variant or explicit evidence attachment; 200 |
| POST | `/api/admin/gear/imports/{id}/reject` | Final rejection with optional notes; 200 |

Offsets are 0–100000. Public list/detail queries exclude Draft/Archived products,
Draft/Archived variants, and models with no published variants. Draft/Archived
performance profiles are omitted. Candidates/review notes are never public.
Images/listings/evidence inherit their owning variant's visibility. Public reads
do not need the admin import APIs. Detail is not paginated in G1; consider child
collection pagination before large-scale ingestion. List intentionally omits
preferred specs/style until a display policy is chosen; detail supplies the data.

**Admin APIs are unauthenticated portfolio-preview endpoints**, consistent with
current Courts moderation. They are not a secured administrative boundary. Do not
treat G1 as secure public ingestion; access control and abuse protection remain
future deployment/product work. G1 adds neither authentication nor infrastructure
configuration.

## Persistence

Migration `20260918105135_AddGearFoundation` creates nine tables only:
`GearBrands`, `GearPaddles`, `GearPaddleVariants`, `GearDataSources`,
`GearSpecificationEvidence`, `GearPaddleListings`, `GearPaddleImages`,
`GearPerformanceProfiles`, `GearImportCandidates`.

The spec value object is owned and stored in typed columns in evidence/candidate
tables, not separate tables. GUID keys are application assigned. All foreign keys
use Restrict deletion so source/evidence/history cannot disappear by cascading.
Constraints cover publication/evidence/source enums, positive dimensions and
weight ranges, price/currency, profile bounds, source/audit timestamps and coherent
candidate decisions. Identity indexes cover normalized brand name, brand slug,
brand/model slug, model/variant slug and optional model/SKU. A filtered unique
index allows at most one primary image per variant. Images order by SortOrder/ID.
Other indexes support public publication/name pagination, import status/date,
variant evidence/date, listings/checked-date and foreign-key lookups.

Listings are appendable checked observations, so later prices can coexist with
earlier ones independently of product identity. No refresh jobs, latest-price
selection policy, seller deduplication or listing mutation API is implemented.
Images, listings and profiles have persistence/read foundations only; their
curation endpoints are deferred. No fake catalog rows are inserted by migration
or startup. No existing Courts/Play tables are changed.

## Synthetic local exercise

Use only a local API backed by a disposable/development database with the G1
migration. This example makes no calls to an external source. It deliberately
uses a manual source and a fictional product. Run once, or use a fresh local
database; repeating publication with the same identity correctly returns 409.

```powershell
$api = 'http://localhost:5000' # replace with the local API's actual port
$observed = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
$source = Invoke-RestMethod "$api/api/admin/gear/sources" -Method Post -ContentType 'application/json' -Body (@{
  type = 'Manual'; name = 'Synthetic G1 smoke fixture'; retrievedAt = $observed
} | ConvertTo-Json)
$candidate = Invoke-RestMethod "$api/api/admin/gear/imports" -Method Post -ContentType 'application/json' -Body (@{
  schemaVersion = 1; sourceId = $source.id
  brandName = 'Kitchain Labs'; brandSlug = 'kitchain-labs'
  paddleName = 'Baseline One'; paddleSlug = 'baseline-one'
  variantName = '16 mm'; variantSlug = '16-mm'
  evidenceType = 'KitchainVerified'; observedAt = $observed
  specifications = @{ thicknessMm = 16; advertisedWeightMinOz = 8; advertisedWeightMaxOz = 8.3 }
} | ConvertTo-Json -Depth 5)
# Inspect the proposal and source before approving these fictional values.
Invoke-RestMethod "$api/api/admin/gear/imports/$($candidate.id)"
Invoke-RestMethod "$api/api/admin/gear/imports/$($candidate.id)/approve" -Method Post -ContentType 'application/json' -Body '{"notes":"Synthetic local fixture only"}'
Invoke-RestMethod "$api/api/gear/paddles"
```

The same versioned contract is the future extraction boundary. It accepts typed
identity and one-source specs only. Unknown fields, performance ratings, prices,
image payloads, raw HTML and extractor-specific confidence metadata do not belong
in version 1. Add reviewed typed contracts for other concepts when needed; never
accept AI output as canonical data or bypass server validation. No AI dependency
or credential is required.

## Verification

```powershell
dotnet build backend/Kitchain.sln
dotnet test backend/Kitchain.sln
Set-Location backend
dotnet ef migrations has-pending-model-changes --project Kitchain.Infrastructure --startup-project Kitchain.Api
Set-Location ../frontend
npm test -- --watch=false
npm run build
npm run lint
npm run format:check
```

Before backend tests, configure `KITCHAIN_TEST_CONNECTION_STRING` with a
**localhost test database** using the existing test workflow. Without it,
PostgreSQL tests are skipped. The reused `PostgresCourtFixture` creates a random
schema, applies migrations and drops only that schema on disposal. Gear tests
cover domain validation, import services, public/API boundaries, concurrent
review, duplicate rollback, provenance, database constraints and migration
rollback/reapply without altering existing module tables/data. Synthetic Gear
rows exist only inside those isolated schemas. Never point this test workflow at
production.

G2 can build catalog/detail clients on these read contracts. Full catalog UI,
search/filters, preferred-value selection, comparisons, recommendation algorithms,
price automation, My Gear, favorites and Gear × Play remain deferred.
